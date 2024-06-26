module TransportTests

open Xunit
open FsUnit
open transportation
open stock;
open events
open api
open FSharp.Control.Reactive

let stock_10pcs_A_1 : StockSpecManifest = { 
     metadata = { 
          name = "10pcs_ofA_on1";
          labels = Some Map.empty
          ``namespace`` = None;
          annotations = None;
          revision = Some "0" };
     spec = { 
          material = "A";
          quantity = "10pcs";
          location = "1";
          }}

let fakeApi<'T when 'T :> Manifest> = {
     new ManifestApi<'T> with
          member _.Get _ = None
          member _.Delete _ = Ok ()
          member _.List _ _ _ = { items = []; continuations = 0 }
          member _.FilterByLabel _ _ _ = { items = []; continuations = 0 }
          member _.Put _ = Ok ()
          member _.WatchFromRevision _ _ = async { return Observable.empty }
          member _.Watch _ = async { return Observable.empty }
     }

let logger = {
     new IEventLogger with
            member _.factory = (fun () -> emptyEvent)
            member _.client = fakeApi<EventSpecManifest>
            member _.Error _ _ = ()
            member _.Debug _ _ = ()
            member _.Info _ _ = ()
            member _.Warn _ _ = ()
}

[<Fact>]
let ``Stocks on source locations are split when the transport is started`` () =
     let transport : TransportFullManifest = { 
          metadata = { 
               name = "transport1";
               labels = None;
               ``namespace`` = None;
               annotations = None;
               revision = Some "0" };
          spec = { 
               material = "A";
               quantity = "1pcs";
               source = Some stock_10pcs_A_1.metadata.name;
               target = "2";
               cancellationRequested = false;
               }
          status = None}
     let stocks = Map.ofList [(stock_10pcs_A_1.metadata.name, stock_10pcs_A_1)]

     let mutable putted = [];

     let stockApi = {
          new ManifestApi<StockSpecManifest> with
               member _.Get _ = None
               member _.Delete _ = Ok ()
               member _.List _ _ _ = { items = []; continuations = 0 }
               member _.FilterByLabel _ _ _ = { items = []; continuations = 0 }
               member _.Put x =
                    putted <- putted @ [x]
                    Ok ()
               member _.WatchFromRevision _ _ = async { return Observable.empty }
               member _.Watch _ = async { return Observable.empty }
     }

     transport_order_controller.startTransport stockApi (Create transport) stocks logger

     putted |> Seq.head |> (fun x -> x.spec.quantity) |> should equal "9.00pcs"
     putted |> Seq.last |> (fun x -> x.spec.quantity) |> should equal "1.00pcs"
     putted |> Seq.last |> (fun x -> x.metadata.labels) |> should equal (Map.ofList [(stockTransportReservation, transport.metadata.name)] |> Some)

[<Fact>]
let ``if a transport already has a stock reserved, not action is taken`` () =
     let transport : TransportFullManifest = { 
          metadata = { 
               name = "transport1";
               labels = None;
               ``namespace`` = None;
               annotations = None;
               revision = Some "0" };
          spec = { 
               material = "A";
               quantity = "1pcs";
               source = Some stock_10pcs_A_1.metadata.name;
               target = "2";
               cancellationRequested = false;
               }
          status = None}
     let reservedStock = {
          stock_10pcs_A_1 with
               metadata.labels = Some (Map.ofList [(stockTransportReservation, transport.metadata.name)])}
     let stocks = Map.ofList [(stock_10pcs_A_1.metadata.name, reservedStock)]

     let mutable putted = [];

     let stockApi = {
          new ManifestApi<StockSpecManifest> with
               member _.Get _ = None
               member _.Delete _ = Ok ()
               member _.List _ _ _ = { items = []; continuations = 0 }
               member _.FilterByLabel _ _ _ = { items = []; continuations = 0 }
               member _.Put x =
                    putted <- putted @ [x]
                    Ok ()
               member _.WatchFromRevision _ _ = async { return Observable.empty }
               member _.Watch _ = async { return Observable.empty }
     }

     transport_order_controller.startTransport stockApi (Create transport) stocks logger

     putted |> should be Empty

[<Fact>]
let ``Stocks are moved to the target location when the transport is completed`` () =
     let transport : TransportFullManifest = { 
          metadata = { 
               name = "transport1";
               labels = None;
               ``namespace`` = None;
               annotations = None;
               revision = Some "0" };
          spec = { 
               material = "A";
               quantity = "1pcs";
               source = Some stock_10pcs_A_1.metadata.name;
               target = "2";
               cancellationRequested = false;
               }
          status = Some { state = "completed" ; reason = None}}
     let reservedStock = {
          stock_10pcs_A_1 with
               metadata.labels = Some (Map.ofList [(stockTransportReservation, transport.metadata.name)])}
     let stocks = Map.ofList [(stock_10pcs_A_1.metadata.name, reservedStock)]

     let mutable putted = [];

     let stockApi = {
          new ManifestApi<StockSpecManifest> with
               member _.Get _ = None
               member _.Delete _ = Ok ()
               member _.List _ _ _ = { items = []; continuations = 0 }
               member _.FilterByLabel _ _ _ = { items = []; continuations = 0 }
               member _.Put x =
                    putted <- putted @ [x]
                    Ok ()
               member _.WatchFromRevision _ _ = async { return Observable.empty }
               member _.Watch _ = async { return Observable.empty }
     }
     
     transport_order_controller.completeTransport logger (Create transport) stocks stockApi

     putted |> Seq.head |> (fun x -> x.spec.location) |> should equal transport.spec.target

[<Fact>]
let ``Reservation labels are removed from stocks once transports are closed`` () =
     let transport : TransportFullManifest = { 
          metadata = { 
               name = "transport1";
               labels = None;
               ``namespace`` = None;
               annotations = None;
               revision = Some "0" };
          spec = { 
               material = "A";
               quantity = "1pcs";
               source = Some stock_10pcs_A_1.metadata.name;
               target = "2";
               cancellationRequested = false;
               }
          status = Some { state = "closed" ; reason = None}}
     let reservedStock = {
          stock_10pcs_A_1 with
               metadata.labels = Some (Map.ofList [(stockTransportReservation, transport.metadata.name)])}
     let stocks = Map.ofList [(stock_10pcs_A_1.metadata.name, reservedStock)]

     let mutable putted = [];

     let stockApi = {
          new ManifestApi<StockSpecManifest> with
               member _.Get _ = None
               member _.Delete _ = Ok ()
               member _.List _ _ _ = { items = []; continuations = 0 }
               member _.FilterByLabel _ _ _ = { items = []; continuations = 0 }
               member _.Put x =
                    putted <- putted @ [x]
                    Ok ()
               member _.WatchFromRevision _ _ = async { return Observable.empty }
               member _.Watch _ = async { return Observable.empty }
     }
     
     transport_order_controller.closeTransport logger (Create transport) stocks stockApi

     putted |> Seq.head |> (fun x -> x.metadata.labels.Value) |> should be Empty

[<Fact>]
let ``Closed Transports are deleted when cleanup runs and they have no reserved stocks`` () =
     let transport : TransportFullManifest = { 
          metadata = { 
               name = "transport1";
               labels = None;
               ``namespace`` = None;
               annotations = None;
               revision = Some "0" };
          spec = { 
               material = "A";
               quantity = "1pcs";
               source = Some stock_10pcs_A_1.metadata.name;
               target = "2";
               cancellationRequested = false;
               }
          status = Some { state = "closed" ; reason = None}}
     let stockApi = fakeApi<StockSpecManifest>
     let mutable deleted = [];
     let transportsApi = {
          new ManifestApi<TransportFullManifest> with
               member _.Get _ = None
               member _.Delete x = 
                    deleted <- deleted @ [x]
                    Ok ()
               member _.List _ _ _ = { items = []; continuations = 0 }
               member _.FilterByLabel _ _ _ = { items = []; continuations = 0 }
               member _.Put _ = Ok ()
               member _.WatchFromRevision _ _ = async { return Observable.empty }
               member _.Watch _ = async { return Observable.empty }
     }

     transport_order_controller.cleanupTransports [stock_10pcs_A_1] [transport] logger transportsApi stockApi
     
     deleted |> should equal [transport.metadata.name]

[<Fact>]
let ``Closed Transports with reserved stocks are deleted after the reservation was removed from the stock`` () =
     let transport : TransportFullManifest = { 
          metadata = { 
               name = "transport1";
               labels = None;
               ``namespace`` = None;
               annotations = None;
               revision = Some "0" };
          spec = { 
               material = "A";
               quantity = "1pcs";
               source = Some stock_10pcs_A_1.metadata.name;
               target = "2";
               cancellationRequested = false;
               }
          status = Some { state = "closed" ; reason = None}}
     let reservedStock = {
          stock_10pcs_A_1 with
               metadata.labels = Some (Map.ofList [(stockTransportReservation, transport.metadata.name)])}
     let mutable updatedStocks = [];
     let stockApi = {
          new ManifestApi<StockSpecManifest> with
               member _.Get _ = None
               member _.Delete _ = Ok ()
               member _.List _ _ _ = { items = []; continuations = 0 }
               member _.FilterByLabel _ _ _ = { items = []; continuations = 0 }
               member _.Put x = 
                    updatedStocks <- updatedStocks @ [x]
                    Ok ()
               member _.WatchFromRevision _ _ = async { return Observable.empty }
               member _.Watch _ = async { return Observable.empty }
     }
     let mutable deleted = [];
     let transportsApi = {
          new ManifestApi<TransportFullManifest> with
               member _.Get _ = None
               member _.Delete x = 
                    deleted <- deleted @ [x]
                    Ok ()
               member _.List _ _ _ = { items = []; continuations = 0 }
               member _.FilterByLabel _ _ _ = { items = []; continuations = 0 }
               member _.Put _ = Ok ()
               member _.WatchFromRevision _ _ = async { return Observable.empty }
               member _.Watch _ = async { return Observable.empty }
     }

     transport_order_controller.cleanupTransports [reservedStock] [transport] logger transportsApi stockApi
     
     deleted |> should equal [transport.metadata.name]
     updatedStocks |> should equal [stock_10pcs_A_1]