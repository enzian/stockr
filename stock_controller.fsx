#r "nuget: dotnet-etcd"
#load "src/Stockr/Stocks.fs"
#load "src/Stockr/Locations.fs"
#load "src/Stockr/Persistence.fs"
#load "src/Stockr/Controller.fs"
open dotnet_etcd
open controller
open System.Threading
open persistence
open stock
open locations

let etcdClient = new EtcdClient("https://localhost:2379")
let locationRepo = newRepository<Location> etcdClient "/locations/"

let handler (curr: SpecType<Stock>) (prev: SpecType<Stock>) =
    let locationTarget = locationRepo.FindById(curr.spec.Location)
    let locationSource = locationRepo.FindById(prev.spec.Location)
    match (locationTarget, locationSource) with
    | (Ok (Some s), Ok (Some t))  ->
        if s.name = t.name then
            printfn "stock had not moved - skipping change" |> ignore
        else
            printfn "resetting is_empty fields on locations"
            locationRepo.Update { t with metadata = { t.metadata with labels = t.metadata.labels |> Map.add "location.stockr.io/v1alpha1/is_empty" "false" }} |> ignore
            locationRepo.Update { s with metadata = { s.metadata with labels = s.metadata.labels |> Map.add "location.stockr.io/v1alpha1/is_empty" "true" }} |> ignore
    | _ -> printfn "no action"
    

let cts = new CancellationTokenSource()

runWatchOnPrefix<SpecType<Stock>> etcdClient handler "/stocks/" cts
    |> Async.RunSynchronously