#r "nuget: FsHttp, 11.0.0"

#load "../Stockr/Api.fs"
#load "../Stockr/Filters.fs"
#load "../Stockr/Stocks.fs"

open System.Net.Http
open System
open stock

// type StockSpec = { material: string; qty : string }
// type StockStatus = { B: string }

let client= new HttpClient()
client.BaseAddress <- new Uri("https://localhost:7243/apis/")

let stockApi = 
    api.ManifestsFor<StockSpecManifest> 
        client
        "logistics.stockr.io/v1alpha1/stock"

let stock: StockSpecManifest = { 
    metadata = { 
        name = "test1"
        ``namespace`` = None
        labels = 
            Some (Map [("locations.stockr.io/footprint", "tub");
             ("locations.stockr.io/ismobile", "false")])
        annotations = None
        revision = None }
    spec = {
        Location = "10-00-001"
        Material = "p146723-11342"
        Amount = { qty = 10.0 ; unit = "pcs"} }
}

stockApi.Put stock
