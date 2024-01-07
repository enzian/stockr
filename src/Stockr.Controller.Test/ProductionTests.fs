module ProductionTests

open Xunit
open FsUnit
open transportation
open stock
open events
open api
open FSharp.Control.Reactive
open production.api
open measurement

let productionOrder: ProductionOrderFullManifest =
    { metadata =
        { name = "test-order-1"
          labels = Some Map.empty
          annotations = Some Map.empty
          ``namespace`` = None
          revision = None }
      spec =
        { material = "resulting-material"
          amount = "10pcs"
          from = "source-location"
          target = "target-location"
          bom =
            [ { material = "material-1"
                quantity = "1pcs" }
              { material = "material-2"
                quantity = "2pcs" } ] }
      status = None }

let stocks: StockSpecManifest seq =
    [ { metadata =
          { name = "material-1"
            labels = None
            annotations = None
            ``namespace`` = None
            revision = None }
        spec =
          { material = "material-1"
            quantity = "100pcs"
            location = "source-location" } }
      { metadata =
          { name = "material-2"
            labels = None
            annotations = None
            ``namespace`` = None
            revision = None }
        spec =
          { material = "material-2"
            quantity = "100pcs"
            location = "source-location" } } ]

let fakeApi<'T when 'T :> Manifest> =
    { new ManifestApi<'T> with
        member _.Get _ = None
        member _.Delete = (fun _ -> Ok())
        member _.List = []
        member _.FilterByLabel = (fun _ -> [])
        member _.Put = (fun _ -> Ok())
        member _.WatchFromRevision = (fun _ _ -> async { return Observable.empty })
        member _.Watch = (fun _ -> async { return Observable.empty }) }

let logger =
    { new IEventLogger with
        member _.factory = (fun () -> emptyEvent)
        member _.client = fakeApi<EventSpecManifest>
        member _.Error _ _ = ()
        member _.Debug _ _ = ()
        member _.Info _ _ = ()
        member _.Warn _ _ = () }

[<Fact>]
let ``For newly created Production Order, new transports should be created.`` () =
    // Act
    let createdTransports =
        production_order_controller.createTransportForNewProductionOrders productionOrder stocks

    // Assert
    createdTransports |> Seq.length |> should equal 2

    let firstTransport = productionOrder.spec.bom |> Seq.head
    let secondTransport = productionOrder.spec.bom |> Seq.last

    createdTransports
    |> Seq.head
    |> (fun t ->
        t.spec.material |> should equal firstTransport.material
        t.spec.quantity |> toMeasure |> should equal (10m, "pcs")
    )

    createdTransports
    |> Seq.last
    |> (fun t ->
        t.spec.material |> should equal secondTransport.material
        t.spec.quantity |> toMeasure |> should equal (20m, "pcs")
    )

[<Fact>]
let ``For newly created Production Order, a transport without a source must be created when there is no stock available`` () =
    let productionOrderWithLowQuantity = { 
        productionOrder with spec.bom = productionOrder.spec.bom @ [ { material = "material-3"; quantity = "1pcs" } ] }

    // Act
    let createdTransports =
        production_order_controller.createTransportForNewProductionOrders productionOrderWithLowQuantity stocks

    // Assert
    createdTransports
    |> Seq.last
    |> (fun t ->
        t.spec.material |> should equal "material-3"
        t.spec.quantity |> toMeasure |> should equal (10m, "pcs")
        t.spec.source |> should equal None
    )