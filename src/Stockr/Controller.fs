module controller
open System.Threading
open dotnet_etcd
open Etcdserverpb
open System
open System.Text
open System.Text.Json

let runWatchOnPrefix<'a> (client: EtcdClient) (handler) (range: string) (cts: CancellationTokenSource) =
    async {
        let handlerFunction (response: WatchResponse) = 
            match response with
            | r when r.Canceled = true -> cts.Cancel()
            | r when r.Events.Count > 0 ->
                for event in r.Events do
                    let curr =
                        event.Kv.Value.ToStringUtf8()
                        |> JsonSerializer.Deserialize<'a>
                    let prev =
                        event.PrevKv.Value.ToStringUtf8()
                        |> JsonSerializer.Deserialize<'a>
                    handler curr prev
            | r -> printfn "No action on: %A" r

        client.WatchRangeAsync(range, Action<_>(handlerFunction), ?cancellationToken = Some cts.Token) |> Async.AwaitTask |> ignore
    }