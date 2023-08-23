module api

open FsHttp
open System.IO
open System.Text.Json
open System.Threading
open System
open System.Net.Http

type Control.Async with
    static member StartDisposable(op: Async<unit>) =
        let ct = new CancellationTokenSource()
        Async.Start(op, ct.Token)

        { new IDisposable with
            member x.Dispose() = ct.Cancel() }


type Metadata =
    { name: string
      ``namespace``: string option
      labels: Map<string, string>
      annotations: Map<string, string>
      revision: string }

type Manifest<'T, 'S> =
    { kind: string
      apigroup: string
      apiversion: string
      metadata: Metadata
      spec: 'T
      status: 'S }

type Event<'T, 'S> =
    | Update of Manifest<'T, 'S>
    | Create of Manifest<'T, 'S>
    | Delete of Manifest<'T, 'S>

type WireEvent<'T, 'S> =
    { ``type``: string
      object: Manifest<'T, 'S> }


type ManifestApi<'TSpec, 'TStatus> =
    abstract Get: string -> Option<Manifest<'TSpec, 'TStatus>>
    abstract List: seq<Manifest<'TSpec, 'TStatus>>
    abstract Watch: (CancellationToken -> Async<IObservable<Event<'TSpec,'TStatus>>>)
    abstract Put: (Manifest<'TSpec, 'TStatus> -> Result<unit, exn>)

let jsonOptions = new JsonSerializerOptions()
jsonOptions.PropertyNameCaseInsensitive <- true

let watchResource<'T, 'S> (client: HttpClient) uri (cts: CancellationToken) =
    async {

        let! responseSteam = client.GetStreamAsync(Path.Combine("watch/", uri)) |> Async.AwaitTask

        let streamReader = new StreamReader(responseSteam)

        let rec readEvent (observer: IObserver<_>) (streamReadr: StreamReader) cts =
            async {
                let! line = streamReadr.ReadLineAsync(cts).AsTask() |> Async.AwaitTask
                observer.OnNext(line)
                return! readEvent observer streamReadr cts
            }

        let lineReaderObservable =
            { new IObservable<_> with
                member x.Subscribe(observer) =
                    readEvent observer streamReader cts |> Async.StartDisposable }

        return
            lineReaderObservable
            |> Observable.map (fun line -> JsonSerializer.Deserialize<WireEvent<'T, 'S>>(line, jsonOptions))
            |> Observable.map (fun wireEvent ->
                match wireEvent.``type`` with
                | "MODIFIED" -> Update wireEvent.object
                | "ADDED" -> Create wireEvent.object
                | "DELETED" -> Delete wireEvent.object
                | _ -> Update wireEvent.object)
    }


let fetchWithKey<'TSpec, 'TStatus> httpClient path resourceKey =
    try
        Some(
            http {
                config_transformHttpClient (fun _ -> httpClient)
                GET(Path.Combine(httpClient.BaseAddress.ToString(), path, resourceKey))
                CacheControl "no-cache"
            }
            |> Request.send
            |> Response.deserializeJson<Manifest<'TSpec, 'TStatus>>
        )
    with e ->
        printfn "%A" e
        None


let listWithKey<'TSpec, 'TStatus> httpClient path =
    try

        http {
            config_transformHttpClient (fun _ -> httpClient)
            GET(Path.Combine(httpClient.BaseAddress.ToString(), path))
        }
        |> Request.send
        |> Response.deserializeJson<seq<Manifest<'TSpec, 'TStatus>>>
    with _ ->
        Seq.empty

let putManifest<'TSpec, 'TStatus> httpClient path (manifest: Manifest<'TSpec, 'TStatus>) =
    try
        http {
            config_transformHttpClient (fun _ -> httpClient)
            PUT (Path.Combine(httpClient.BaseAddress.ToString(), path))
            body
            jsonSerialize manifest
        }
        |> Request.send |> ignore
        Ok ()
    with e ->
        Error e

let ManifestsFor<'TSpec, 'TStatus> (httpClient: HttpClient) (path: string) =
    { new ManifestApi<'TSpec, 'TStatus> with
        member _.Get key =
            fetchWithKey httpClient path key
        member _.List =
            listWithKey httpClient path
        member _.Watch =
            watchResource httpClient path
        member _.Put = putManifest httpClient path
    }
