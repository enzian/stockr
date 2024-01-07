module transportation

open api
open measurement

let stockTransportReservation = "logistics.stockr.io/transport-order"

module api = 
    let Version = "v1alpha1"
    let Group = "logistics.stockr.io"
    let Kind = "transport"

module reasons =
    let NoStockWithSufficientQuantity = Some "NoStockWithSufficientQuantity"

type TransportSpec = {
    material: string
    quantity: string
    source: string option
    target: string
    cancellationRequested: bool
}

type TransportStatus = {
    state: string
    reason: string option
}

type TransportSpecManifest = 
    { spec: TransportSpec
      metadata: Metadata }
    interface Manifest with 
        member this.metadata = this.metadata

type TransportFullManifest = 
    { status: TransportStatus option
      spec: TransportSpec
      metadata: Metadata }
    interface Manifest with 
        member this.metadata = this.metadata
