#r "nuget: FsHttp, 11.0.0"
#r "nuget: FSharpx.Async, 1.14.1"
#r "nuget: FSharp.Control.Reactive, 5.0.5"


#load "../Stockr/Filters.fs"
#load "../Stockr/Api.fs"
#load "../Stockr/Stocks.fs"
#load "../Stockr/Locations.fs"

open System.Net.Http
open System
open stock
open System.Threading
open locations
open api
open FSharp.Control.Reactive

let client= new HttpClient()
client.BaseAddress <- new Uri("https://localhost:7243/apis/")

let stockApi = 
    api.ManifestsFor<StockSpecManifest> 
        client
        "logistics.stockr.io/v1alpha1/stock"
let locationsApi = 
    api.ManifestsFor<LocationSpecManifest> 
        client
        "logistics.stockr.io/v1alpha1/location"
let cts = new CancellationTokenSource()
// let t = 
//     async {
//         let! result = stockApi.Watch cts.Token
//         // result.Subscribe((fun x -> printfn "%A"  x)) |> ignore
//         result.Subscribe({ new IObserver<_> with
//                               member x.OnNext(v) = printfn "%A" v
//                               member x.OnError(e) = printf "observable failed: %A" e
//                               member x.OnCompleted() = printf "observable completed" 
//                         }) |> ignore
//     }
//     |> Async.RunSynchronously


let stocksWatch = stockApi.Watch cts.Token |> Async.RunSynchronously
let stocks = stockApi.List
let locationsWatch = locationsApi.Watch cts.Token |> Async.RunSynchronously
let locations = locationsApi.List

let appendToDict<'T when 'T :> Manifest> (d: Map<string, 'T>) (e: Event<'T>) =
    match e with
    | Update m ->
        d.Add(m.metadata.name, m)
    | Create m -> d.Add(m.metadata.name, m)
    | Delete m -> 
        d.Remove (m.metadata.name)

let initialStockObs = Subject.behavior (
    stocks |> Seq.map (fun x -> (x.metadata.name, x)) |> Map.ofSeq
)
let allStocks =
    Observable.merge
        (stocksWatch
        |> Observable.scanInit (stocks |> Seq.map (fun x -> (x.metadata.name, x)) |> Map.ofSeq) appendToDict)
        initialStockObs

let initialLocationsObs = Subject.behavior (
    locations |> Seq.map (fun x -> (x.metadata.name, x)) |> Map.ofSeq
)
let allLocations =
    Observable.merge 
        (locationsWatch
            |> Observable.scanInit (locations |> Seq.map (fun x -> (x.metadata.name, x)) |> Map.ofSeq) appendToDict)
        initialLocationsObs

(Observable.combineLatest allLocations allStocks )
    .Subscribe((fun (locs, stocks) -> 
        let locationIds = locs.Values |> Seq.map (fun x -> x.spec.Id)
        let phantomStock = 
            stocks.Values
            |> Seq.filter (fun x -> locationIds |> Seq.contains x.spec.Location |> not)
            |> Seq.map (fun x -> x.metadata.name)
        printfn "Found phantom stock: %A"  phantomStock
    ))