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

let client= new HttpClient()
client.BaseAddress <- new Uri("https://localhost:7243/apis/")

let locationApi = ManifestsFor<locations.Location, unit> client "logistics.stockr.io/v1alpha1/location"

let stockApi = ManifestsFor<stock.Stock, unit> client "logistics.stockr.io/v1alpha1/stock"

locationApi.Put {
    kind = "location"
    apigroup = "logistics.stockr.io"
    apiversion = "v1alpha1"
    metadata = {
        name = "10-00-001"
        labels = None
        annotations = None
        revision = None
        ``namespace`` = None
    }
    spec = Some { Id = "10-00-001" }
    status = None
}
locationApi.Put {
    kind = "location"
    apigroup = "logistics.stockr.io"
    apiversion = "v1alpha1"
    metadata = {
        name = "10-01-001"
        labels = None
        annotations = None
        revision = None
        ``namespace`` = None
    }
    spec = Some { Id = "10-01-001" }
    status = None
}

stockApi.Put {
    kind = "stock"
    apigroup = "logistics.stockr.io"
    apiversion = "v1alpha1"
    metadata = {
        name = "10-01-001"
        labels = None
        annotations = None
        revision = None
        ``namespace`` = None
    }
    spec = Some { 
        Location = "10-01-001"
        Material = Material "A"
        Amount = {
            qty = Quantity 12.0
            unit = Unit "pcs" }
     }
    status = None
}
