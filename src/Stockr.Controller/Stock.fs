module stock

open api

module api = 
  let Version = "v1alpha1"
  let Group = "stocks.stockr.io"
  let Kind = "stock" 

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