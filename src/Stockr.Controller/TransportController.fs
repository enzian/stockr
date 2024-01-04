module transport_order_controller

open System.Threading
open api
open stock
open transportation
open FSharp.Control.Reactive.Observable
open measurement
open events
open System


let inline toMap kvps =
    kvps |> Seq.map (|KeyValue|) |> Map.ofSeq

let AddInitalTransportStatus (apiClient: ManifestApi<TransportFullManifest>) x =
    match x |> Seq.tryHead with
    | Some transport ->
        let transportWithStatus = { transport with status = Some { state = "created" } }
        apiClient.Put transportWithStatus |> ignore
    | None -> ()

let startTransport 
    (stocksApi : ManifestApi<StockSpecManifest>)
    (x : Event<TransportFullManifest>)
    (stocks : Map<string, StockSpecManifest>)
    (logger : IEventLogger) = 
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
        
        let objectReference : ObjectReference = { 
            kind = transportation.apiKind
            group = transportation.apiGroup
            name = x.metadata.name
            apiVersion = transportation.apiVersion
            resourceVersion = x.metadata.revision.Value }
        let logger = logger |> customize (fun e -> e |> regarding objectReference  )

        if existingTransportStock.IsSome then
            logger.Info
                "StockExists"
                (sprintf
                    "Stock for transport %s already exists: %s, skipping stocks split."
                    x.metadata.name
                    existingTransportStock.Value)
        else
            logger.Info
                "NoDedicatedStockExists"
                (sprintf "Stock for transport %s does not exist, splitting stock..." x.metadata.name)

            let stock = stocks |> Map.find x.spec.source
            let (Some (stockQty, stockUnit)) = stock.spec.quantity |> amountFromString
            let (Some (transportQty, transportUnit)) = x.spec.quantity |> amountFromString

            let updatedStock =
                { stock with
                    spec.quantity =
                        (stockQty - (transportQty |> convert transportUnit stockUnit), stockUnit) |> toString }

            stocksApi.Put updatedStock |> ignore

            let newStock =
                { stock with
                    metadata.name = x.metadata.name
                    metadata.labels =
                        stock.metadata.labels
                        |> Option.defaultValue Map.empty
                        |> Map.add stockTransportReservation x.metadata.name
                        |> Some
                    spec.quantity = (transportQty, transportUnit) |> toString }

            match stocksApi.Put newStock with
            | Ok _ ->
                logger.Info
                    "StockSplit"
                    (sprintf
                        "Stock %s split into %s and %s"
                        stock.metadata.name
                        stock.metadata.name
                        newStock.metadata.name)
            | Error e -> logger.Error "StockSplitError" (sprintf "Failed to split stock: \n%A" e)
    | _ -> ()


let completeTransport 
    logger 
    (event: Event<TransportFullManifest>)
    (stocks : Map<string, StockSpecManifest>)
    (stocksApi : ManifestApi<StockSpecManifest>) = 
    let transport =
        match event with
        | Update x
        | Create x -> x
        | _ -> failwith "Invalid event"

    let logObjRef event =
        event |> regarding
            { kind = transportation.apiKind
              group = transportation.apiGroup
              name = transport.metadata.name
              apiVersion = transportation.apiVersion
              resourceVersion = transport.metadata.revision.Value }
    let logger = logger |> customize (logObjRef)

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
        logger.Info
            "TransportCompleted"
            (sprintf
                "Transport %s completed, moving stock %s to target location %s"
                transport.metadata.name
                stock.metadata.name
                transport.spec.target)

        let movedStock =
            { stock with spec = { stock.spec with location = transport.spec.target } }

        match stocksApi.Put movedStock with 
        | Ok _ ->
            logger.Info
                "StockMoved"
                (sprintf
                    "Stock %s moved to %s"
                    stock.metadata.name
                    transport.spec.target)
        | Error e ->
            logger.Error
                "StockMovementError"
                (sprintf
                    "Failed to move stock %s to %s: \n%A"
                    stock.metadata.name
                    transport.spec.target
                    e)
    | None -> 
        logger.Info
            "TransportStockMissing"
            (sprintf
                "Transport %s completed, but no stock found"
                transport.metadata.name)

let closeTransport 
    logger
    event
    (stocks : Map<string, StockSpecManifest>)
    (stocksApi : ManifestApi<StockSpecManifest>) = 
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
    
    let logObjRef event =
        event |> regarding
            { kind = transportation.apiKind
              group = transportation.apiGroup
              name = transport.metadata.name
              apiVersion = transportation.apiVersion
              resourceVersion = transport.metadata.revision.Value }
    let log = logger |> customize (logObjRef)

    match stock with
    | Some stock ->
        log.Info
            "TransportClosed"
            (sprintf
                "Transport %s closed, removing transport labels from stock %s"
                transport.metadata.name
                stock.metadata.name)

        let movedStock =
            { stock with
                metadata =
                    { stock.metadata with
                        labels =
                            stock.metadata.labels
                            |> Option.defaultValue Map.empty
                            |> Map.remove stockTransportReservation
                            |> Some } }

        match stocksApi.Put movedStock with
        | Ok _ ->
            log.Info
                "TransportReservationRemoved"
                (sprintf
                    "Transport labels removed from stock %s"
                    stock.metadata.name)
        | Error e ->
            log.Error
                "TransportReservationRemovalError"
                (sprintf
                    "Failed to remove transport labels from stock %s: \n%A"
                    stock.metadata.name
                    e)

    | None -> 
        log.Warn
            "NoStockFound"
            (sprintf
                "Transport %s closed, but no corresponding stock found"
                transport.metadata.name)

let runController (ct: CancellationToken) client =
    async {
        printfn "Starting TransportOrderController"

        let transportsApi =
            ManifestsFor<TransportFullManifest>
                client
                (sprintf "%s/%s/%s/" transportation.apiGroup transportation.apiVersion transportation.apiKind)

        let stocksApi =
            ManifestsFor<StockSpecManifest> client (sprintf "%s/%s/%s/" stock.apiGroup stock.apiVersion stock.apiKind)

        let (aggregateTransports, transportChanges) =
            utilities.watchResourceOfType transportsApi ct

        let (aggregateStocks, _) = utilities.watchResourceOfType stocksApi ct

        let eventsApi =
            ManifestsFor<EventSpecManifest> client (sprintf "%s/%s/%s/" events.apiGroup events.apiVersion events.apiKind)

        let eventFactory () =
            emptyEvent
            |> withCurrentTime
            |> withComponent "stockr-controller"
            |> withReportingInstance (System.Net.Dns.GetHostName())
        let logger = newEventLogger eventsApi eventFactory

        let statusTransition from target (change, transports) =
            match change with
            | Update x ->
                let existingTransport = transports |> Map.find x.metadata.name

                existingTransport.status.IsSome
                && x.status.IsSome
                && existingTransport.status.Value.state = from
                && x.status.Value.state = target
            | Create x -> x.status.IsSome && x.status.Value.state = target
            | _ -> false
        
        aggregateTransports
        |> combineLatest transportChanges
        |> filter (statusTransition "created" "started")
        |> withLatestFrom (fun a b -> (b, a)) aggregateStocks
        |> subscribe (fun (stocks, (x, _)) -> startTransport stocksApi x stocks logger )
        |> ignore

        aggregateTransports
        |> combineLatest transportChanges
        |> filter (statusTransition "started" "completed")
        |> withLatestFrom (fun a b -> (b, a)) aggregateStocks
        |> subscribe (fun (stocks, (event, _)) -> completeTransport logger event stocks stocksApi)
        |> ignore

        aggregateTransports
        |> combineLatest transportChanges
        |> filter (statusTransition "completed" "closed")
        |> withLatestFrom (fun a b -> (b, a)) aggregateStocks
        |> subscribe (fun (stocks, (event, _)) -> closeTransport logger event stocks stocksApi )
        |> ignore
    
        interval (TimeSpan.FromSeconds(5.0))
        |> combineLatest (aggregateTransports |> map (fun x -> x.Values))
        |> combineLatest (aggregateStocks |> map (fun x -> x.Values))
        |> subscribe (fun (stocks, (transports, _)) -> 
            let closedTransports = 
                transports
                |> Seq.filter (fun x -> x.status.IsSome && x.status.Value.state = "closed")
            for transport in closedTransports do
                let reservedStock = 
                    stocks
                    |> Seq.filter (fun x -> 
                        x.metadata.labels
                        |> Option.defaultValue Map.empty
                        |> Map.tryFind stockTransportReservation
                        |> Option.exists ((=) transport.metadata.name))
                    |> Seq.tryHead
                let logger = 
                    logger 
                    |> customize (fun e -> 
                        e 
                        |> regarding { kind = transportation.apiKind
                                       group = transportation.apiGroup
                                       name = transport.metadata.name
                                       apiVersion = transportation.apiVersion
                                       resourceVersion = transport.metadata.revision.Value })

                let deleteTransport transport =
                    match transportsApi.Delete transport with
                    | Ok _ ->
                        logger.Info
                            "TransportRemoved"
                            (sprintf
                                "Transport %s removed because it has been closed and no stock is reserved to it"
                                transport)
                    | Error e ->
                        logger.Error
                            "TransportRemovalError"
                            (sprintf
                                "Failed to remove transport %s: \n%A"
                                transport
                                e)
                match reservedStock with
                | Some stock ->
                    let logger = logger |> customize (fun e -> 
                        e 
                        |> regarding { kind = transportation.apiKind
                                       group = transportation.apiGroup
                                       name = transport.metadata.name
                                       apiVersion = transportation.apiVersion
                                       resourceVersion = transport.metadata.revision.Value })
                    logger.Info
                        "TransportHasReservedStock"
                        (sprintf
                            "Transport %s cannot be removed because it still has stock with a reservation to it: %s"
                            transport.metadata.name
                            stock.metadata.name)
                    let releasedStock = 
                        { stock with
                            metadata =
                                { stock.metadata with
                                    labels =
                                        stock.metadata.labels
                                        |> Option.defaultValue Map.empty
                                        |> Map.remove stockTransportReservation
                                        |> Some } }
                    match stocksApi.Put releasedStock with
                    | Ok _ ->
                        logger.Info
                            "TransportReservationRemoved"
                            (sprintf
                                "Transport labels removed from stock %s"
                                stock.metadata.name)
                    | Error e ->
                        logger.Error
                            "TransportReservationRemovalError"
                            (sprintf
                                "Failed to remove transport labels from stock %s: \n%A"
                                stock.metadata.name
                                e)
                    deleteTransport transport.metadata.name
                | None ->
                    deleteTransport transport.metadata.name
                 
        ) |> ignore

        (Async.AwaitWaitHandle ct.WaitHandle) |> Async.RunSynchronously |> ignore
        printfn "Stopped TransportOrderController"
    }
