module controller

open System.Threading
open System.Net.Http
open System.IO
open System.Text.Json
open System

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


let jsonOptions = new JsonSerializerOptions()
jsonOptions.PropertyNameCaseInsensitive <- true

type Control.Async with

    static member StartDisposable(op: Async<unit>) =
        let ct = new CancellationTokenSource()
        Async.Start(op, ct.Token)

        { new IDisposable with
            member x.Dispose() = ct.Cancel() }

let watchResource<'T, 'S> (client: HttpClient) uri (cts: CancellationToken) =
    async {

        let! responseSteam = client.GetStreamAsync(Path.Combine("apis/watch/", uri)) |> Async.AwaitTask

        let streamReader = new StreamReader(responseSteam)

        let rec readEvent (observer: IObserver<_>) (streamReadr: StreamReader) cts =
            async {
                let! line = streamReadr.ReadLineAsync(cts).AsTask() |> Async.AwaitTask
                observer.OnNext(line)
                return! readEvent observer streamReadr cts
            }
        // try

        //     while (not streamReader.EndOfStream) && (not cts.IsCancellationRequested) do
        //         let! line = streamReader.ReadLineAsync() |> Async.AwaitTask
        //         let wireEvent = JsonSerializer.Deserialize<WireEvent<'T, 'S>> (line, jsonOptions)
        //         let event =
        //             match wireEvent.``type`` with
        //             | "MODIFIED" -> Update wireEvent.object
        //             | "ADDED" -> Create wireEvent.object
        //             | "DELETED" -> Delete wireEvent.object
        //             | _ -> Update wireEvent.object

        //         do! handler event
        // with
        // | :? IOException -> printfn "connection closed by host"

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
