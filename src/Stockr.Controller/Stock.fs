module stock

open api

type StockSpec = {
    material: string
    quantity: string
    location: string
}

type StockSpecManifest = 
    { spec: StockSpec
      metadata: Metadata }
    interface Manifest with 
        member this.metadata = this.metadata 