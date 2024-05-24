module location

open api

let apiVersion = "v1alpha1"
let apiGroup = "logistics.stockr.io"
let apiKind = "location"

type LocationSpec = {
    id: string
}

type LocationSpecManifest = 
    { spec: LocationSpec
      metadata: Metadata }
    interface Manifest with 
        member this.metadata = this.metadata 