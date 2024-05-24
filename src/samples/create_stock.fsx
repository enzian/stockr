#r "nuget: Manifesto.Client.Fsharp"

#load "../Stockr.Controller/Stock.fs"

open System.Net.Http
open System
open stock

let client= new HttpClient()
client.BaseAddress <- new Uri("http://localhost:5000/apis/")

let stockApi = 
    api.ManifestsFor<StockSpecManifest> 
        client
        "stocks.stockr.io/v1alpha1/stock/"

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
        location = "10-00-001"
        material = "p146723-11342"
        quantity = "10pcs" }
}

stockApi.Put stock
