module production_order_controller

open System.Threading
open api
open FSharp.Control.Reactive.Observable
open measurement
open transportation
open stock
open events
open production.api
open production



// Constants
[<Literal>]
let productionOrderLabel = "logistics.stockr.io/production-order"

[<Literal>]
let sourceStockLabel = "logistics.stockr.io/from-stock"

// let multiplyBom po =
//     let (d, u) = po.amount |> toMeasure

//     po.bom
//     |> List.map (fun x ->
//         let (di, u) = x.quantity |> toMeasure
//         let factor = di * d

//         {| x with
//             quantity = Measure(factor, u) |})

let findStockWithQuantity (stocks: StockSpecManifest seq) material quantity =
    stocks
    |> Seq.filter (fun x -> x.spec.material = material)
    |> Seq.filter (fun x ->
        let stockAmount = x.spec.quantity |> toMeasure
        let (stockQty, stockUnit) = stockAmount
        let (qtyRequest, uRequest) = quantity

        if (stockUnit = uRequest) then
            stockQty >= qtyRequest
        else
            let (d, u) = stockAmount |> convertMeasure uRequest
            d >= qtyRequest)
    |> Seq.tryHead

let sumTransportQuantities unit (transports: TransportSpecManifest seq) =
    transports
    |> Seq.map (fun x ->
        let (d, u) = x.spec.quantity |> toMeasure |> convertMeasure unit
        d)
    |> Seq.sum

let createTransportForNewProductionOrders
    (productionOrder: ProductionOrderFullManifest)
    (availableStock: StockSpecManifest seq)
    =
    let productionLines = productionOrder.spec.bom
    let pqty, _ = productionOrder.spec.amount |> toMeasure

    let transportsWithoutSource: (TransportSpecManifest seq) =
        productionLines
        |> Seq.map (fun x ->
            let d, u = x.quantity |> toMeasure

            {| x with
                quantity = Measure(d * pqty, u) |})
        |> Seq.map (fun x ->
            { metadata =
                { name = sprintf "%s-%s" productionOrder.metadata.name (utilities.randomStr 7)
                  labels = Some(Map.ofList [ productionOrderLabel, productionOrder.metadata.name ])
                  ``namespace`` = None
                  annotations = None
                  revision = None }
              spec =
                { source = None
                  material = x.material
                  quantity = x.quantity |> measureToString
                  target = productionOrder.spec.from
                  cancellationRequested = false } })

    let transportsWithSource: TransportFullManifest seq =
        transportsWithoutSource
        |> Seq.map (fun x ->
            let stock =
                findStockWithQuantity availableStock x.spec.material (x.spec.quantity |> toMeasure)

            match stock with
            | Some stock ->
                { metadata = x.metadata
                  spec = { x.spec with source = Some stock.metadata.name }
                  status = Some { state = "created" ; reason = None } }
            | None ->
                { metadata = x.metadata
                  spec = { x.spec with source = None }
                  status = Some { state = "pending" ; reason = Some "" } })

    transportsWithSource


let runController (ct: CancellationToken) client =
    async {
        printfn "Starting ProductionOrderController"

        let prodOrderApi =
            ManifestsFor<production.api.ProductionOrderFullManifest>
                client
                (sprintf "%s/%s/%s/" apiGroup Version apiKind)

        let transportsApi =
            ManifestsFor<TransportFullManifest>
                client
                (sprintf "%s/%s/%s/" transportation.api.Group transportation.api.Version transportation.api.Kind)

        let transportsStatusApi =
            ManifestsFor<TransportFullManifest>
                client
                (sprintf "%s/%s/%s/status" transportation.api.Group transportation.api.Version transportation.api.Kind)

        let stocksApi =
            ManifestsFor<StockSpecManifest> client (sprintf "%s/%s/%s/" stock.apiGroup stock.apiVersion stock.apiKind)

        let eventsApi =
            ManifestsFor<EventSpecManifest>
                client
                (sprintf "%s/%s/%s/" events.apiGroup events.apiVersion events.apiKind)

        let eventFactory () =
            emptyEvent
            |> withCurrentTime
            |> withComponent "stockr-controller"
            |> withReportingInstance (System.Net.Dns.GetHostName())

        let logger = newEventLogger eventsApi eventFactory


        //setup the rective pipelines that feed data from the resource API.
        let (aggregateProdOrders, productionOrderEvents) =
            utilities.watchResourceOfType prodOrderApi ct

        let (aggregateTransports, _) = utilities.watchResourceOfType transportsApi ct
        let (aggregateStocks, _) = utilities.watchResourceOfType stocksApi ct

        productionOrderEvents
        |> choose (fun x ->
            match x with
            | Create po -> Some po
            | _ -> None)
        |> withLatestFrom (fun x y -> (x, y)) aggregateStocks
        |> map (fun (order, stocks) ->
            let newTransports = createTransportForNewProductionOrders order stocks.Values
            (order, newTransports))
        |> subscribe (fun (order, transports) ->
            let logObjRef event =
                event
                |> relatedTo
                    { kind = apiKind
                      group = apiGroup
                      name = order.metadata.name
                      apiVersion = Version
                      resourceVersion = order.metadata.revision.Value }
            let logger = logger |> customize (logObjRef)

            for transport in transports do
                let logObjRef event =
                    event
                    |> regarding
                        { kind = transportation.api.Kind
                          group = transportation.api.Group
                          name = transport.metadata.name
                          apiVersion = transportation.api.Version
                          resourceVersion = transport.metadata.revision |> Option.defaultValue "" }
                let logger = logger |> customize (logObjRef)
                match transportsApi.Put transport with
                | Ok () ->
                    logger.Info
                        "TransportCreated"
                        (sprintf
                            "Created transport %s for production order %s"
                            transport.metadata.name
                            order.metadata.name)
                | Error err ->
                    logger.Warn
                        "TransportCreationFailed"
                        (sprintf "Could not create transport for production order %s: %A"
                            order.metadata.name err)

            logger.Info 
                "TransportsCreated"
                        (sprintf
                            "Created transports production order %s"
                            order.metadata.name)
        )
        |> ignore


        // let productionLines =
        //     aggregateProdOrders
        //     |> map (fun x ->
        //         x.Values
        //         |> Seq.filter (fun x -> x.status.IsSome && x.status.Value.state <> "cancelled")
        //         |> Seq.map (fun prodOrder ->
        //             let productionAmount = prodOrder.spec.amount |> amountFromString

        //             match productionAmount with
        //             | Some (d, u) ->
        //                 prodOrder.spec.bom |> List.map (fun x ->
        //                     let di, u = x.quantity |> toMeasure
        //                     let factor = di * d
        //                     {| x with
        //                         quantity = (factor, u)
        //                         prod_order = prodOrder.metadata.name
        //                         takes_from = prodOrder.spec.from |}
        //                 )
        //             | None ->
        //                 failwithf "Could not parse amount %s on production order %s" prodOrder.spec.amount prodOrder.metadata.name
        //         )
        //         |> Seq.concat
        //     )

        // let productionLinesWithTransports =
        //     productionLines
        //     |> combineLatest aggregateTransports
        //     |> map ( fun (transportMap, lines) ->
        //         let transports = transportMap.Values
        //         lines |> Seq.map (fun x ->
        //             let transportForLine = transports |> Seq.filter( fun t ->
        //                 t.metadata.labels.IsSome &&
        //                 t.metadata.labels.Value.ContainsKey productionOrderLabel &&
        //                 t.metadata.labels.Value[productionOrderLabel] = x.prod_order &&
        //                 t.spec.material = x.material)
        //             {| x with transports = transportForLine |}
        //         ))
        //     |> map ( fun (lines) ->
        //         lines
        //         |> Seq.map (fun l ->
        //             let (_, lineunit) = l.quantity
        //             let totalLineQty = l.transports |> Seq.map (fun x -> x.spec.quantity |> toMeasure |> convertMeasure lineunit) |> Seq.map (fun (qty, _) -> qty) |> Seq.sum
        //             {|l with transport_qty = totalLineQty|})
        //         )

        // productionLinesWithTransports
        // |> map (fun lines ->
        //     lines
        //     |> Seq.filter (fun x -> x.transport_qty < fst x.quantity)
        // )
        // |> filter (fun x -> x |> Seq.isEmpty |> not)
        // |> combineLatest aggregateStocks
        // |> subscribe (fun (stocks, bomLines) ->
        //     let bomLine = bomLines |> Seq.head

        //     let withRegardsTo event =
        //         event |> regarding
        //             { kind = transportation.apiKind
        //               group = transportation.apiGroup
        //               name = bomLine.prod_order
        //               apiVersion = transportation.apiVersion
        //               resourceVersion = "" }
        //     let logger = logger |> customize withRegardsTo

        //     let perspectiveStocks = findStockWithQuantity (stocks.Values |> Seq.cast) bomLine.material (Measure (bomLine.quantity))
        //     logger.Info
        //         "LowQuantity"
        //         (sprintf "Bom Line has low quantity: %A \n Perspective Stocks to pick from: %A" bomLine (perspectiveStocks |> Option.toList |> List.map _.metadata.name))

        //     match perspectiveStocks with
        //     | Some stock ->
        //         let (lineQty, lineUnit) = bomLine.quantity
        //         let transportManifest : TransportFullManifest = {
        //             metadata = {
        //                 name = sprintf "%s-%s" bomLine.prod_order (utilities.randomStr 7)
        //                 labels = Some (Map.ofList [productionOrderLabel, bomLine.prod_order; sourceStockLabel, stock.metadata.name])
        //                 ``namespace`` = None
        //                 annotations = None
        //                 revision = None}
        //             spec = {
        //                 material = bomLine.material
        //                 quantity = (Measure (lineQty - bomLine.transport_qty, lineUnit) |> measureToString)
        //                 source = stock.metadata.name
        //                 target = bomLine.takes_from
        //                 cancellationRequested = false }
        //             status = Some { state = "created" } }
        //         match transportsApi.Put transportManifest with
        //         | Ok () ->
        //             logger.Info
        //                 "TransportCreated"
        //                 (sprintf "Created transport %s for BOM line %A" transportManifest.metadata.name bomLine)
        //         | Error err ->
        //             logger.Warn
        //                 "TransportCreationFailed"
        //                 (sprintf "Could not create transport for bom line %A: %A" bomLine err)
        //     | None ->
        //         logger.Warn
        //             "NoStockFound"
        //             (sprintf "No stock found for bom line %A - cannot create transport" bomLine)
        // ) |> ignore

        // productionLinesWithTransports
        // |> map (fun lines ->
        //     lines
        //     |> Seq.filter (fun line -> line.transport_qty > fst line.quantity))
        // |> filter (fun x -> x |> Seq.isEmpty |> not)
        // |> subscribe(fun lines ->
        //     let x = lines |> Seq.head
        //     let withRegardsTo event =
        //         event |> regarding
        //             { kind = transportation.apiKind
        //               group = transportation.apiGroup
        //               name = x.prod_order
        //               apiVersion = transportation.apiVersion
        //               resourceVersion = "" }
        //     let logger = logger |> customize withRegardsTo
        //     logger.Debug
        //         "HighQuantity"
        //         (sprintf "Bom Line has high quantity: %A must create a compensation transport" x)
        //     // TODO: create a compenstation transport
        // ) |> ignore

        // productionOrderEvents
        // |> choose (fun x -> match x with | Create po | Update po -> Some po | _ -> None)
        // |> filter (fun x -> match x.status with | Some status when status.state = "cancelled" -> true | _ -> false)
        // |> withLatestFrom (fun x y -> (x,y)) aggregateTransports
        // |> map (fun (x, y) -> (x,y.Values))
        // |> map (fun (po, transports) ->
        //     let transports =
        //         transports
        //         |> Seq.filter (fun t -> t.metadata.labels |> Option.exists (fun l -> l.ContainsKey productionOrderLabel && l[productionOrderLabel] = po.metadata.name))
        //     (po, transports))
        // |> filter (fun (_, transports) -> transports |> Seq.isEmpty |> not)
        // |> subscribe (fun (po, transports) ->
        //     for transport in transports do
        //         let logObjRef event =
        //             event |> regarding
        //                 { kind = transportation.apiKind
        //                   group = transportation.apiGroup
        //                   name = transport.metadata.name
        //                   apiVersion = transportation.apiVersion
        //                   resourceVersion = transport.metadata.revision.Value }
        //                 |> relatedTo
        //                 { kind = apiKind
        //                   group = apiGroup
        //                   name = po.metadata.name
        //                   apiVersion = apiVersion
        //                   resourceVersion = po.metadata.revision.Value }
        //         let logger = logger |> customize (logObjRef)

        //         match transport.status with
        //         | Some status when status.state = "closed" ->
        //             logger.Info
        //                 "TransportClosed"
        //                 (sprintf "Transport %s was already closed, skipping." transport.metadata.name)
        //         | Some status when status.state = "created" ->
        //             let closedTransport = { transport with status = Some { state = "closed" } }
        //             match transportsStatusApi.Put closedTransport with
        //             | Ok () ->
        //                 logger.Info
        //                     "TransportClosed"
        //                     (sprintf "Transport %s was closed." transport.metadata.name)
        //             | Error err ->
        //                 logger.Warn
        //                     "TransportClosureFailed"
        //                     (sprintf "Could not close transport %s: %A" transport.metadata.name err)
        //         | Some status when status.state = "started" ->
        //             let cancelledTransport = { transport with spec.cancellationRequested = true }
        //             match transportsApi.Put cancelledTransport with
        //             | Ok () ->
        //                 logger.Info
        //                     "TransportCancelled"
        //                     (sprintf "Transport %s was cancelled." transport.metadata.name)
        //             | Error err ->
        //                 logger.Warn
        //                     "TransportCancellationFailed"
        //                     (sprintf "Could not cancel transport %s: %A" transport.metadata.name err)
        //         | Some status when status.state = "completed" || status.state = "closed" ->
        //             let labels = transport.metadata.labels |> Option.defaultValue Map.empty |> Map.filter (fun k _ -> k <> productionOrderLabel)
        //             let returnTransport = {
        //                 transport with
        //                     metadata.labels = Some labels
        //                     metadata.name = (sprintf "%s-" (utilities.randomStr 8))
        //                     spec = {
        //                         transport.spec with
        //                             source = transport.spec.target
        //                             target = transport.spec.source }
        //                     status = Some { state = "created" }}
        //             match transportsApi.Put returnTransport with
        //             | Ok () ->
        //                 logger.Info
        //                     "ReturnTransportCreated"
        //                     (sprintf "Return Transport %s was created b/c production order %s was deleted" transport.metadata.name po.metadata.name)
        //             | Error err ->
        //                 logger.Warn
        //                     "ReturnTransportCreationFailed"
        //                     (sprintf "Could not create transport %s: %A" transport.metadata.name err)
        //         | None ->
        //             match transportsApi.Delete transport.metadata.name with
        //             | Ok () ->
        //                 logger.Info
        //                     "TransportDeleted"
        //                     (sprintf "Transport %s was deleted b/c production order %s was deleted" transport.metadata.name po.metadata.name)
        //             | Error err ->
        //                 logger.Warn
        //                     "TransportDeletionFailed"
        //                     (sprintf "Could not delete transport %s: %A" transport.metadata.name err)
        //         | _ -> ()

        // ) |> ignore

        (Async.AwaitWaitHandle ct.WaitHandle) |> Async.RunSynchronously |> ignore
        printfn "Stopped ProductionOrderController"
    }
