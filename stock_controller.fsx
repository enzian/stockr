#r "nuget: dotnet-etcd"
#load "src/Stockr/Stocks.fs"
#load "src/Stockr/Locations.fs"
#load "src/Stockr/Persistence.fs"
#load "src/Stockr/Controller.fs"
open dotnet_etcd
open controller
open System.Threading
open persistence

let etcdClient = new EtcdClient("https://localhost:2379")
let locationRepo = LocationRepo etcdClient

let handler (curr: StockModel) (prev: StockModel) =
    let locationTarget = locationRepo.FindById(curr.Location)
    let locationSource = locationRepo.FindById(prev.Location)
    match (locationTarget, locationSource) with
    | (Ok (Some s), Ok (Some t))  ->
        if s.Id = t.Id then
            printfn "stock had not moved - skipping change" |> ignore
        else
            printfn "resetting is_empty fields on locations"
            locationRepo.Update { t with Labels = t.Labels |> Map.add "location.stockr.io/v1alpha1/is_empty" "false" } |> ignore
            locationRepo.Update { s with Labels = s.Labels |> Map.add "location.stockr.io/v1alpha1/is_empty" "true" } |> ignore
    | _ -> printfn "no action"
    

let cts = new CancellationTokenSource()

System.AppDomain.CurrentDomain.ProcessExit.Add(fun _ -> cts.Cancel())

runWatchOnPrefix<StockModel> etcdClient handler "/stocks/" cts
    |> Async.RunSynchronously