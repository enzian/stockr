module transport

open api
open measurement

let stockTransportReservation = "logistics.stockr.io/transport-order"

type TransportSpec = {
    material: string
    quantity: string
    source: string
    target: string
}

type TransportStatus = {
    state: string
}

type TransportSpecManifest = 
    { spec: TransportSpec
      metadata: Metadata }
    interface Manifest with 
        member this.metadata = this.metadata

type Transport = {
    material: string
    quantity: Measure
    from: string
    target: string
}

type TransportFullManifest = 
    { status: TransportStatus option
      spec: TransportSpec
      metadata: Metadata }
    interface Manifest with 
        member this.metadata = this.metadata

