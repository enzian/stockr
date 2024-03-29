module stock_controller

open System.Threading
open api
open location
open stock
open FSharp.Control.Reactive.Observable
open events

let runController (ct: CancellationToken) client =
    async {
        printfn "Starting ProductionOrderController"

        let stocksApi =
            ManifestsFor<StockSpecManifest> client (sprintf "%s/%s/%s/" stock.apiGroup stock.apiVersion stock.apiKind)
        let locationsApi =
            ManifestsFor<LocationsSpecManifest> client (sprintf "%s/%s/%s/" location.apiGroup location.apiVersion location.apiKind)

        //setup the rective pipelines that feed data from the resource API.
        let (locations, _) = utilities.watchResourceOfType locationsApi ct
        let (aggregateStocks, _) = utilities.watchResourceOfType stocksApi ct

        locations
        |> combineLatest aggregateStocks
        |> subscribe (fun (stocks, locations) ->
            let phantomStock =
                stocks.Values
                |> Seq.filter (fun x -> locations.Values |> Seq.exists (fun y -> y.metadata.name = x.spec.location) |> not)
                |> Seq.tryHead

            match phantomStock with
            | Some x ->
                let locationSpec : LocationsSpecManifest =
                    { metadata =
                        { name = x.spec.location
                          labels = None
                          ``namespace`` = None
                          annotations = None
                          revision = None }
                      spec = { id = x.spec.location } }

                locationsApi.Put locationSpec |> ignore
            | None -> ()
        )
        |> ignore

        ()
    }
