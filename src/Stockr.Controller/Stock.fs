module stock

open api

let apiVersion = "v1alpha1"
let apiGroup = "stocks.stockr.io"
let apiKind = "stock" 

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