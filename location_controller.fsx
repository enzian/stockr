#r "nuget: dotnet-etcd"
#load "src/Stockr/Stocks.fs"
#load "src/Stockr/Locations.fs"
#load "src/Stockr/Persistence.fs"
#load "src/Stockr/Controller.fs"
open dotnet_etcd
open controller
open System.Threading
open persistence
open System.Text.Json

let etcdClient = new EtcdClient("https://localhost:2379")

let handler (curr: LocationModel) (prev: LocationModel) =
    printfn "Handling %A %A" (curr |> JsonSerializer.Serialize) (prev.Id |> JsonSerializer.Serialize)

let cts = new CancellationTokenSource()

runWatchOnPrefix<LocationModel> etcdClient handler "/locations/" cts
    |> Async.RunSynchronously