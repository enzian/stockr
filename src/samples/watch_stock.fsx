// #r "nuget: FsHttp, 11.0.0"
#r "nuget: Manifesto.Client.Fsharp"
#r "nuget: FSharpx.Async, 1.14.1"
#r "nuget: FSharp.Control.Reactive, 5.0.5"


#load "../Stockr.Controller/Stock.fs"
#load "../Stockr.Controller/Location.fs"

open System.Net.Http
open System
open stock
open System.Threading
open location
open api
open FSharp.Control.Reactive

let client= new HttpClient()
client.BaseAddress <- new Uri("http://localhost:5000/apis/")

let stockApi = 
    api.ManifestsFor<StockSpecManifest> 
        client
        "stocks.stockr.io/v1alpha1/stock/"
let locationsApi = 
    api.ManifestsFor<LocationSpecManifest> 
        client
        "logistics.stockr.io/v1alpha1/location/"
let cts = new CancellationTokenSource()


let stocksWatch = stockApi.Watch cts.Token |> Async.RunSynchronously
let stocks = stockApi.List CancellationToken.None 0 1000
let locationsWatch = locationsApi.Watch cts.Token |> Async.RunSynchronously
let locations = locationsApi.List CancellationToken.None 0 1000

let appendToDict<'T when 'T :> Manifest> (d: Map<string, 'T>) (e: Event<'T>) =
    match e with
    | Update m ->
        d.Add(m.metadata.name, m)
    | Create m -> d.Add(m.metadata.name, m)
    | Delete m -> 
        d.Remove (m.metadata.name)

let initialStockObs = Subject.behavior (
    stocks.items |> Seq.map (fun x -> (x.metadata.name, x)) |> Map.ofSeq
)
let allStocks =
    Observable.merge
        (stocksWatch
        |> Observable.scanInit (stocks.items |> Seq.map (fun x -> (x.metadata.name, x)) |> Map.ofSeq) appendToDict)
        initialStockObs

let initialLocationsObs = Subject.behavior (
    locations.items |> Seq.map (fun x -> (x.metadata.name, x)) |> Map.ofSeq
)
let allLocations =
    Observable.merge 
        (locationsWatch
            |> Observable.scanInit (locations.items |> Seq.map (fun x -> (x.metadata.name, x)) |> Map.ofSeq) appendToDict)
        initialLocationsObs

(Observable.combineLatest allLocations allStocks )
    .Subscribe((fun (locs, stocks) -> 
        let locationIds = locs.Values |> Seq.map (fun x -> x.spec.id)
        let phantomStock = 
            stocks.Values
            |> Seq.filter (fun x -> locationIds |> Seq.contains x.spec.location |> not)
            |> Seq.map (fun x -> x.metadata.name)
        printfn "Found phantom stock: %A"  phantomStock
    ))