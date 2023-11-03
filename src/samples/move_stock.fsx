#r "nuget: FsHttp, 11.0.0"

#load "../Stockr/Api.fs"
#load "../Stockr/Locations.fs"
#load "../Stockr/Stocks.fs"
#load "../Stockr/Persistence.fs"
#load "../Stockr/Filters.fs"
#load "../Stockr/Logistics.fs"

open System.Net.Http
open System
open api
open stock
open logistics
open locations

let client = new HttpClient()
client.BaseAddress <- new Uri("https://localhost:7243/apis/")

let locationApi =
    ManifestsFor<LocationSpecManifest> client "logistics.stockr.io/v1alpha1/location"

let stockApi =
    ManifestsFor<StockManifest> client "logistics.stockr.io/v1alpha1/stock"

locationApi.Put
    { metadata =
        { name = "10-00-001"
          labels = None
          annotations = None
          revision = None
          ``namespace`` = None }
      spec = { Id = "10-00-001" } }

locationApi.Put
    { metadata =
        { name = "10-01-001"
          labels = None
          annotations = None
          revision = None
          ``namespace`` = None }
      spec = { Id = "10-01-001" } }

stockApi.Put
    { metadata =
        { name = "10-01-001"
          labels = None
          annotations = None
          revision = None
          ``namespace`` = None }
      spec =
        { Location = "10-01-001"
          Material = "A"
          Amount =
            { qty = 12.0
              unit = "pcs" } } }

MoveQuantity stockApi
