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
          labels = None
          ``namespace`` = None;
          annotations = None;
          revision = Some "0" };
     spec = { 
          material = "A";
          quantity = "10pcs";
          location = "1";
          }}

let fakeApi <'T when 'T :> Manifest> = {
     new ManifestApi<'T> with
          member _.Get _ = None
          member _.Delete = (fun _ -> Ok ())
          member _.List = []
          member _.FilterByLabel = (fun _ -> [])
          member _.Put = (fun _ -> Ok ())
          member _.WatchFromRevision = (fun _ _ -> async { return Observable.empty })
          member _.Watch = (fun _ -> async { return Observable.empty })
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
               source = stock_10pcs_A_1.metadata.name;
               target = "2";
               cancellationRequested = false;
               }
          status = None}
     let stocks = Map.ofList [(stock_10pcs_A_1.metadata.name, stock_10pcs_A_1)]

     let mutable putted = [];

     let stockApi = {
          new ManifestApi<StockSpecManifest> with
               member _.Get _ = None
               member _.Delete = (fun _ -> Ok ())
               member _.List = []
               member _.FilterByLabel = (fun _ -> [])
               member _.Put = (fun x ->
                    putted <- putted @ [x]
                    Ok ())
               member _.WatchFromRevision = (fun _ _ -> async { return Observable.empty })
               member _.Watch = (fun _ -> async { return Observable.empty })
     }

     transport_order_controller.startTransport stockApi (Create transport) stocks logger
     putted |> Seq.head |> (fun x -> x.spec.quantity) |> should equal "9.00pcs"
     putted |> Seq.last |> (fun x -> x.spec.quantity) |> should equal "1.00pcs"
     putted |> Seq.last |> (fun x -> x.metadata.labels) |> should equal (Map.ofList [(stockTransportReservation, transport.metadata.name)] |> Some)

[<Fact>]
let ``Adding all quantities must return sum of all transport quantities`` () =
     ()