module transport_order_controller

open System.Threading
open api
open stock
open transport
open FSharp.Control.Reactive.Observable
open measurement

let inline toMap kvps =
    kvps |> Seq.map (|KeyValue|) |> Map.ofSeq

let AddInitalTransportStatus (apiClient: ManifestApi<TransportFullManifest>) x =
    match x |> Seq.tryHead with
    | Some transport ->
        let transportWithStatus = { transport with status = Some { state = "created" } }
        apiClient.Put transportWithStatus |> ignore
    | None -> ()

let runController (ct: CancellationToken) client =
    async {
        printfn "Starting TransportOrderController"

        let transportsApi =
            ManifestsFor<TransportFullManifest> client "logistics.stockr.io/v1alpha1/transports/"
        let transportsStatusApi =
            ManifestsFor<TransportFullManifest> client "logistics.stockr.io/v1alpha1/transports/status"
        let stocksApi =
            ManifestsFor<StockSpecManifest> client "stocks.stockr.io/v1alpha1/stocks/"
        let (aggregateTransports, transportChanges) =
            utilities.watchResourceOfType transportsApi ct
        let (aggregateStocks, _) = utilities.watchResourceOfType stocksApi ct

        aggregateTransports
        |> combineLatest transportChanges
        |> filter (fun (change, transports) ->
            match change with
            | Update x ->
                let existingTransport = transports |> Map.find x.metadata.name
                existingTransport.status.IsSome
                    && x.status.IsSome
                    && existingTransport.status.Value.state = "created"
                    && x.status.Value.state = "started"
            | Create x -> x.status.IsSome && x.status.Value.state = "started"
            | _ -> false)
        |> withLatestFrom (fun a b -> (b, a)) aggregateStocks
        |> subscribe (fun (stocks, (x, aggregateTransports)) ->
            match x with
            | Update x
            | Create x ->
                let existingTransportStock =
                    stocks
                    |> Map.tryFindKey (fun k v ->
                        v.metadata.labels
                        |> Option.defaultValue Map.empty
                        |> Map.tryFind stockTransportReservation
                        |> Option.exists ((=) x.metadata.name))

                if existingTransportStock.IsSome then
                    printfn
                        "Stock for transport %s already exists: %s, skipping stocks split."
                        x.metadata.name
                        existingTransportStock.Value
                else
                    printfn "Transport %s started, spliting stock..." x.metadata.name
                    let stock = stocks |> Map.find x.spec.source
                    let (Some (stockQty, stockUnit)) = stock.spec.quantity |> amountFromString
                    let (Some (transportQty, transportUnit)) = x.spec.quantity |> amountFromString

                    let updatedStock =
                        { stock with
                            spec =
                                { stock.spec with
                                    quantity =
                                        (stockQty - (transportQty |> convert transportUnit stockUnit), stockUnit)
                                        |> toString } }

                    stocksApi.Put updatedStock |> ignore

                    let newStock =
                        { stock with
                            metadata.name = sprintf "%s-%s" x.metadata.name stock.metadata.name
                            metadata.labels =
                                stock.metadata.labels
                                |> Option.defaultValue Map.empty
                                |> Map.add stockTransportReservation x.metadata.name
                                |> Some
                            spec.quantity = (transportQty, transportUnit) |> toString }

                    stocksApi.Put newStock |> ignore

            | _ -> ())
        |> ignore

        aggregateTransports
        |> combineLatest transportChanges
        |> filter (fun (change, transports) ->
            match change with
            | Update x ->
                let existingTransport = transports |> Map.find x.metadata.name
                existingTransport.status.IsSome
                    && x.status.IsSome
                    && existingTransport.status.Value.state = "started"
                    && x.status.Value.state = "completed"
            | Create x -> x.status.IsSome && x.status.Value.state = "completed"
            | _ -> false)
        |> withLatestFrom (fun a b -> (b, a)) aggregateStocks
        |> subscribe (fun (stocks, (event, _)) ->
            let transport =
                match event with
                | Update x
                | Create x -> x
                | _ -> failwith "Invalid event"

            let stock =
                stocks
                |> Map.values
                |> Seq.tryFind (fun x ->
                    x.metadata.labels
                    |> Option.defaultValue Map.empty
                    |> Map.tryFind stockTransportReservation
                    |> Option.exists ((=) transport.metadata.name))

            match stock with
            | Some stock ->
                printfn
                    "Transport %s completed, moving stock %s to target location %s"
                    transport.metadata.name
                    stock.metadata.name
                    transport.spec.target

                let movedStock =
                    { stock with spec = { stock.spec with location = transport.spec.target } }

                stocksApi.Put movedStock |> ignore
            | None -> printfn "Transport %s completed, but no stock found" transport.metadata.name

            ())
        |> ignore

        aggregateTransports
        |> combineLatest transportChanges
        |> filter (fun (change, transports) ->
            match change with
            | Update x ->
                let existingTransport = transports |> Map.find x.metadata.name
                existingTransport.status.IsSome
                    && x.status.IsSome
                    && existingTransport.status.Value.state = "completed"
                    && x.status.Value.state = "closed"
            | Create x -> x.status.IsSome && x.status.Value.state = "closed"
            | _ -> false)
        |> withLatestFrom (fun a b -> (b, a)) aggregateStocks
        |> subscribe (fun (stocks, (event, _)) ->
            let transport =
                match event with
                | Update x
                | Create x -> x
                | _ -> failwith "Invalid event"

            let stock =
                stocks
                |> Map.values
                |> Seq.tryFind (fun x ->
                    x.metadata.labels
                    |> Option.defaultValue Map.empty
                    |> Map.tryFind stockTransportReservation
                    |> Option.exists ((=) transport.metadata.name))

            match stock with
            | Some stock ->
                printfn
                    "Transport %s closed, removing transport labels from stock %s"
                    transport.metadata.name
                    stock.metadata.name

                let movedStock =
                    { stock with
                        metadata =
                            { stock.metadata with
                                labels =
                                    stock.metadata.labels
                                    |> Option.defaultValue Map.empty
                                    |> Map.remove stockTransportReservation
                                    |> Some } }

                stocksApi.Put movedStock |> ignore
            | None -> printfn "Transport %s closed, but no corresponding stock found" transport.metadata.name)
        |> ignore

        (Async.AwaitWaitHandle ct.WaitHandle) |> Async.RunSynchronously |> ignore
        printfn "Stopped TransportOrderController"
    }
