module controller

open System.Threading
open System.Net.Http
open System.IO
open System.Text.Json

type Metadata =
    { name: string
      ``namespace``: string option
      labels: Map<string, string>
      annotations: Map<string, string>
      revision: string }

type TestSpec = { A: string }

type Manifest<'T> =
    { kind: string
      apigroup: string
      apiversion: string
      metadata: Metadata
      spec: 'T }

type Event<'T> =
    | Update of Manifest<'T>
    | Create of Manifest<'T>
    | Delete of Manifest<'T>

type WireEvent<'T> = {
    ``type``: string
    object: Manifest<'T>
}


let jsonOptions = new JsonSerializerOptions()
jsonOptions.PropertyNameCaseInsensitive <- true

let watchResource<'T> (client: HttpClient) uri (handler) (cts: CancellationToken) =
    async {
        let! responseSteam =
            client.GetStreamAsync(Path.Combine("apis/watch/", uri))
            |> Async.AwaitTask

        let streamReader = new StreamReader(responseSteam)

        while (not streamReader.EndOfStream) && (not cts.IsCancellationRequested) do
            let! line = streamReader.ReadLineAsync() |> Async.AwaitTask
            let wireEvent = JsonSerializer.Deserialize<WireEvent<'T>> (line, jsonOptions)
            printfn "Type: %A" wireEvent
            let event =
                match wireEvent.``type`` with
                | "MODIFIED" -> Update wireEvent.object
                | "ADDED" -> Create wireEvent.object
                | "DELETED" -> Delete wireEvent.object
                | _ -> Update wireEvent.object

            do! handler event
    }
