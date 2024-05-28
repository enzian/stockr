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

let defaultProdOutputStock (prodOrder: ProductionOrderFullManifest) : stock.StockSpecManifest =
    { metadata =
        { name = $"prod-{prodOrder.metadata.name}"
          labels = Some(Map.ofList [ productionOrderLabel, prodOrder.metadata.name ])
          annotations = None
          revision = None
          ``namespace`` = None }
      spec =
        { location = prodOrder.spec.target
          material = prodOrder.spec.material
          quantity = "0pcs" } }

let findStockWithQuantity (stocks: StockSpecManifest seq) material quantity =
    stocks
    |> Seq.filter (fun x -> x.spec.material = material)
    |> Seq.filter (fun x ->
        let stockAmount = x.spec.quantity |> toQuantity
        let (stockQty, stockUnit) = stockAmount
        let (qtyRequest, uRequest) = quantity

        if (stockUnit = uRequest) then
            stockQty >= qtyRequest
        else
            let (d, u) = stockAmount |> convertQuantity uRequest
            d >= qtyRequest)
    |> Seq.tryHead

let sumTransportQuantities unit (transports: TransportSpecManifest seq) =
    transports
    |> Seq.map (fun x ->
        let (d, u) = x.spec.quantity |> toQuantity |> convertQuantity unit
        d)
    |> Seq.sum

let createTransportForNewProductionOrders
    (productionOrder: ProductionOrderFullManifest)
    (availableStock: StockSpecManifest seq)
    =
    let productionLines = productionOrder.spec.bom
    let pqty, _ = productionOrder.spec.amount |> toQuantity

    let transportsWithoutSource: (TransportSpecManifest seq) =
        productionLines
        |> Seq.map (fun x ->
            let d, u = x.quantity |> toQuantity

            {| x with
                quantity = Quantity(d * pqty, u) |})
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
                  quantity = x.quantity |> quantityToString
                  target = productionOrder.spec.from
                  cancellationRequested = false } })

    let transportsWithSource: TransportFullManifest seq =
        transportsWithoutSource
        |> Seq.map (fun x ->
            let stock =
                findStockWithQuantity availableStock x.spec.material (x.spec.quantity |> toQuantity)

            match stock with
            | Some stock ->
                { metadata = x.metadata
                  spec = { x.spec with source = Some stock.metadata.name }
                  status = Some { state = "created"; reason = None } }
            | None ->
                { metadata = x.metadata
                  spec = { x.spec with source = None }
                  status = Some { state = "pending"; reason = Some "" } })

    transportsWithSource


let runController (ct: CancellationToken) client =
    async {
        printfn "Starting ProductionOrderController"

        let prodOrderApi =
            ManifestsFor<production.api.ProductionOrderFullManifest>
                client
                (sprintf "%s/%s/%s/" production.api.Group production.api.Version production.api.Kind)

        let transportsApi =
            ManifestsFor<TransportFullManifest>
                client
                (sprintf "%s/%s/%s/" transportation.api.Group transportation.api.Version transportation.api.Kind)

        let transportsStatusApi =
            ManifestsFor<TransportFullManifest>
                client
                (sprintf "%s/%s/%s/status" transportation.api.Group transportation.api.Version transportation.api.Kind)

        let stocksApi =
            ManifestsFor<StockSpecManifest>
                client
                (sprintf "%s/%s/%s/" stock.api.Group stock.api.Version stock.api.Kind)

        let eventsApi =
            ManifestsFor<EventSpecManifest>
                client
                (sprintf "%s/%s/%s/" events.api.Group events.api.Version events.api.Kind)

        let eventFactory () =
            emptyEvent
            |> withCurrentTime
            |> withComponent "stockr-controller"
            |> withReportingInstance (System.Net.Dns.GetHostName())

        let logger = newEventLogger eventsApi eventFactory


        //setup the reactive pipelines that feed data from the resource API.
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
                    { kind = production.api.Kind
                      group = production.api.Group
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
                        (sprintf "Could not create transport for production order %s: %A" order.metadata.name err)

            logger.Info "TransportsCreated" (sprintf "Created transports production order %s" order.metadata.name))
        |> ignore


        productionOrderEvents
        |> choose (fun x ->
            match x with
            | Update po -> Some po
            | _ -> None)
        |> filter (fun x -> x.status |> Option.isSome)
        |> withLatestFrom (fun x y -> (x, y)) aggregateStocks
        |> map (fun (prodOrder, stocks) ->
            let (_, prodUnit) = prodOrder.spec.amount |> toQuantity
            let (prodQty, prodUnit) = if prodOrder.status.IsSome then prodOrder.status.Value.amount |> toQuantity else (0M, prodUnit)

            let existingOutputStocks =
                stocks.Values
                |> Seq.filter (fun x ->
                    x.metadata.labels
                    |> Option.defaultValue Map.empty
                    |> Map.tryPick (fun k v ->
                        if k = productionOrderLabel && v = prodOrder.metadata.name then
                            Some v
                        else
                            None)
                    |> Option.defaultValue ""
                    |> (fun x -> x = prodOrder.metadata.name))

            let totalQtyPresent =
                existingOutputStocks
                |> Seq.map (fun x -> x.spec.quantity |> toQuantity)
                |> Seq.map (fun (v, u) -> convert u prodUnit v)
                |> Seq.sum

            let designatedOutputStock =
                existingOutputStocks
                |> Seq.filter (fun x -> x.spec.location = prodOrder.spec.target)
                |> Seq.tryHead
                |> Option.defaultValue (defaultProdOutputStock prodOrder)

            if totalQtyPresent < prodQty then
                let diffQty = prodQty - totalQtyPresent

                let (outputStockAmount, outputStockUnit) =
                    designatedOutputStock.spec.quantity |> toQuantity

                { designatedOutputStock with
                    spec.quantity = (outputStockAmount + diffQty, outputStockUnit) |> quantityToString }
            else
                designatedOutputStock)
        |> subscribe (fun stock ->
            match stocksApi.Put stock with
            | Ok _ -> ()
            | Error e -> eprintfn "Error updating stock: %s" (e |> string))
        |> ignore


        (Async.AwaitWaitHandle ct.WaitHandle) |> Async.RunSynchronously |> ignore
        printfn "Stopped ProductionOrderController"
    }
