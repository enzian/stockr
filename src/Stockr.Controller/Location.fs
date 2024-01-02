module location

open api

type LocationSpec = {
    id: string
}

type LocationsSpecManifest = 
    { spec: LocationSpec
      metadata: Metadata }
    interface Manifest with 
        member this.metadata = this.metadata 