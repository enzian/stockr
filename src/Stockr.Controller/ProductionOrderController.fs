module production_order_controller
open System.Threading
open api
open FSharp.Control.Reactive.Observable
open measurement
open transport
open stock
open System.Security.Cryptography

type ProductionLine = {
    material: string
    quantity: string
}

type ProductionOrder = {
    bom: ProductionLine list
    material: string
    amount: string
    from: string
    target: string
}

type ProductionOrderSpecManifest = 
    { spec: ProductionOrder
      metadata: Metadata }
    interface Manifest with 
        member this.metadata = this.metadata 

// Constants
let productionOrderLabel = "logistics.stockr.io/production-order"
let sourceStockLabel = "logistics.stockr.io/from-stock"

let multiplyBom po =
    let (Measure (d, u)) = po.amount |> toMeasure
    po.bom |> List.map (fun x -> 
        let (Measure (di, u)) = x.quantity |> toMeasure
        let factor = di * d
        {| x with quantity = Measure (factor, u) |}
    )

let findStockWithQuantity (stocks: StockSpecManifest seq) material quantity = 
    stocks
    |> Seq.filter (fun x -> x.spec.material = material) 
    |> Seq.filter (fun x -> 
        let stockAmount = x.spec.quantity |> toMeasure
        let (Measure (stockQty, stockUnit)) = stockAmount
        let (Measure (qtyRequest, uRequest)) = quantity
        if (stockUnit = uRequest) then
            stockQty >= qtyRequest
        else
            let (Measure (d, u)) = stockAmount |> convertMeasure uRequest
            d >= qtyRequest
    )
    |> Seq.tryHead

let sumTransportQuantities unit (transports: TransportSpecManifest seq) = 
    transports
    |> Seq.map (fun x -> 
        let (Measure (d, u)) = x.spec.quantity |> toMeasure |> convertMeasure unit
        d
    )
    |> Seq.sum

let runController (ct: CancellationToken) client =
    async {
        printfn "Starting ProductionOrderController"
        let prodOrderApi = ManifestsFor<ProductionOrderSpecManifest> client "logistics.stockr.io/v1alpha1/production-orders/"
        let transportsApi = ManifestsFor<TransportSpecManifest> client "logistics.stockr.io/v1alpha1/transports/"
        let stocksApi = ManifestsFor<StockSpecManifest> client "stocks.stockr.io/v1alpha1/stocks/"

        //setup the rective pipelines that feed data from the resource API.
        let (aggregateProdOrders, watchObs) = utilities.watchResourceOfType prodOrderApi ct
        let (aggregateTransports, _) = utilities.watchResourceOfType transportsApi ct
        let (aggregateStocks, _) = utilities.watchResourceOfType stocksApi ct

        let productionLines = aggregateProdOrders |> map (fun x -> 
            x.Values |> Seq.map (fun prodOrder -> 
                let productionAmount = prodOrder.spec.amount |> amountFromString
                
                match productionAmount with
                | Some (d, u) -> 
                    prodOrder.spec.bom |> List.map (fun x -> 
                        let (Measure (di, u)) = x.quantity |> toMeasure
                        let factor = di * d
                        {| x with 
                            quantity = (factor, u)
                            prod_order = prodOrder.metadata.name
                            takes_from = prodOrder.spec.from |}
                    ) 
                | None -> 
                    failwithf "Could not parse amount %s on production order %s" prodOrder.spec.amount prodOrder.metadata.name
            )
            |> Seq.concat
        )

        let productionLinesWithTransports = 
            productionLines
            |> combineLatest aggregateTransports
            |> map ( fun (transportMap, lines) -> 
                let transports = transportMap.Values
                lines |> Seq.map (fun x -> 
                    let transportForLine = transports |> Seq.filter( fun t -> 
                        t.metadata.labels.IsSome && 
                        t.metadata.labels.Value.ContainsKey productionOrderLabel &&
                        t.metadata.labels.Value[productionOrderLabel] = x.prod_order &&
                        t.spec.material = x.material)
                    {| x with transports = transportForLine |}
                ))
            |> flatmap (fun x ->  x |> toObservable)
            |> map ( fun (l) -> 
                let (_, lineunit) = l.quantity
                let totalLineQty = l.transports |> Seq.map (fun x -> x.spec.quantity |> toMeasure |> convertMeasure lineunit) |> Seq.map (fun (Measure (qty, _)) -> qty) |> Seq.sum
                {|l with transport_qty = totalLineQty|})
            
        let bomLinesLowTransportQty = 
            productionLinesWithTransports 
            |> filter (fun x -> x.transport_qty < fst x.quantity)
    
        bomLinesLowTransportQty
        |> combineLatest aggregateStocks
        |> distinctKey (fun (_, bomline) -> bomline)
        |> subscribe (fun (stocks, bomLine) ->
            let perspectiveStocks = findStockWithQuantity (stocks.Values |> Seq.cast) bomLine.material (Measure (bomLine.quantity))
            printfn "Bom Line has low quantity: %A \n Perspective Stocks to pick from: %A" bomLine (perspectiveStocks |> Option.toList |> List.map _.metadata.name)
            match perspectiveStocks with
            | Some stock -> 
                printfn "Creating transport from %s to %s" stock.metadata.name bomLine.takes_from
                let (lineQty, lineUnit) = bomLine.quantity
                let transportManifest : TransportSpecManifest = { 
                    metadata = { 
                        name = sprintf "%s-%s" bomLine.prod_order stock.metadata.name
                        labels = Some (Map.ofList [productionOrderLabel, bomLine.prod_order; sourceStockLabel, stock.metadata.name])
                        ``namespace`` = None
                        annotations = None
                        revision = None}
                    spec = {
                        material = bomLine.material
                        quantity = (Measure (lineQty - bomLine.transport_qty, lineUnit) |> measureToString)
                        source = stock.metadata.name
                        target = bomLine.takes_from } }
                transportsApi.Put transportManifest |> ignore
            | None ->
                printfn "No stock found for bom line %A - cannot create transport" bomLine
        ) |> ignore
        
        let bomLinesHighTransportQty = productionLinesWithTransports |> filter (fun x -> x.transport_qty > fst x.quantity)
        (bomLinesHighTransportQty.Subscribe(
            fun x -> printfn "Bom Line has high quantity: %A must create a compensation transport" x 
            ))|> ignore
        
        (Async.AwaitWaitHandle ct.WaitHandle) |> Async.RunSynchronously |> ignore
        printfn "Stopped ProductionOrderController"
    }
