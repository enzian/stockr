#r "nuget: Manifesto.Client.Fsharp"

#load "../Stockr.Controller/Location.fs"
#load "../Stockr.Controller/Stock.fs"
#load "../Stockr.Controller/Logistics.fs"

open System.Net.Http
open System
open api
open stock
open location

let client = new HttpClient()
client.BaseAddress <- new Uri("http://localhost:5000/apis/")

let locationApi =
    ManifestsFor<LocationSpecManifest> client "logistics.stockr.io/v1alpha1/location/"

let stockApi =
    ManifestsFor<StockSpecManifest> client "stocks.stockr.io/v1alpha1/stock/"

locationApi.Put
    { metadata =
        { name = "10-00-001"
          labels = None
          annotations = None
          revision = None
          ``namespace`` = None }
      spec = { id = "10-00-001" } }

locationApi.Put
    { metadata =
        { name = "10-01-001"
          labels = None
          annotations = None
          revision = None
          ``namespace`` = None }
      spec = { id = "10-01-001" } }

let stock : StockSpecManifest = 
  { metadata =
        { name = "10-01-001"
          labels = None
          annotations = None
          revision = None
          ``namespace`` = None }
    spec =
      { location = "10-01-001"
        material = "A"
        quantity = "10pcs" }}
stockApi.Put stock
    

logistics.MoveQuantity stockApi stock "10-01-001"
