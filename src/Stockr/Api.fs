module api

open FsHttp
open System.IO
open System.Text.Json
open System.Threading
open System
open System.Net.Http
open filter

type Control.Async with

    static member StartDisposable(op: Async<unit>) =
        let ct = new CancellationTokenSource()
        Async.Start(op, ct.Token)

        { new IDisposable with
            member x.Dispose() = ct.Cancel() }


type Metadata =
    { name: string
      ``namespace``: string option
      labels: Map<string, string> option
      annotations: Map<string, string> option
      revision: string option }

type Manifest<'T, 'S> =
    { kind: string
      apigroup: string
      apiversion: string
      metadata: Metadata
      spec: 'T option
      status: 'S option }

type Event<'T, 'S> =
    | Update of Manifest<'T, 'S>
    | Create of Manifest<'T, 'S>
    | Delete of Manifest<'T, 'S>

type WireEvent<'T, 'S> =
    { ``type``: string
      object: Manifest<'T, 'S> }


let formatLabelFilter condition =
    match condition with
    | (k, Eq v ) -> sprintf "%s=%s" k v
    | (k, NotEq v ) ->sprintf "%s!=%s" k v
    | (k, Set) -> sprintf "%s" k
    | (k, NotSet) -> sprintf "!%s" k
    | (k, In values) -> sprintf "%s in (%s)" k (values |> String.concat ",")
    | (k, NotIn values) -> sprintf "%s in (%s)" k (values |> String.concat ",")

type ManifestApi<'TSpec, 'TStatus> =
    abstract Get: string -> Option<Manifest<'TSpec, 'TStatus>>
    abstract List: seq<Manifest<'TSpec, 'TStatus>>
    abstract FilterByLabel: (KeyIs seq -> Manifest<'TSpec, 'TStatus> seq)
    abstract Watch: (CancellationToken -> Async<IObservable<Event<'TSpec, 'TStatus>>>)
    abstract Put: (Manifest<'TSpec, 'TStatus> -> Result<unit, exn>)
    abstract Delete: (string -> Result<unit, exn>)

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
            PUT(Path.Combine(httpClient.BaseAddress.ToString(), path))
            body
            jsonSerialize manifest
        }
        |> Request.send
        |> ignore

        Ok()
    with e ->
        Error e

let listWithFilter<'TSpec, 'TStatus> httpClient path (keyIs: KeyIs seq) =
    try
        let filter = keyIs |> Seq.map formatLabelFilter |> String.concat ","
        http {
            config_transformHttpClient (fun _ -> httpClient)
            GET(Path.Combine(httpClient.BaseAddress.ToString(), path))
            query [("filter", filter)]
        }
        |> Request.send
        |> Response.deserializeJson<seq<Manifest<'TSpec, 'TStatus>>>
    with _ ->
        Seq.empty

let dropManifest httpClient path key =
    try
        http {
            config_transformHttpClient (fun _ -> httpClient)
            DELETE (Path.Combine(httpClient.BaseAddress.ToString(), path, key))
        }
        |> Request.send
        |> ignore
        Ok ()
    with e ->
        Error e

let ManifestsFor<'TSpec, 'TStatus> (httpClient: HttpClient) (path: string) =
    { new ManifestApi<'TSpec, 'TStatus> with
        member _.Get key = fetchWithKey httpClient path key
        member _.List = listWithKey httpClient path
        member _.FilterByLabel = listWithFilter httpClient path
        member _.Watch = watchResource httpClient path
        member _.Put = putManifest httpClient path 
        member _.Delete = dropManifest httpClient path }
