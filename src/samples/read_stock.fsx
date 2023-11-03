#r "nuget: FsHttp, 11.0.0"

#load "../Stockr/Filters.fs"
#load "../Stockr/Api.fs"
#load "../Stockr/Stocks.fs"

open System.Net.Http
open System
open filter
open stock

let client= new HttpClient()
client.BaseAddress <- new Uri("https://localhost:7243/apis/")

let stockApi = 
    api.ManifestsFor<StockSpecManifest> 
        client
        "logistics.stockr.io/v1alpha1/stock"

// List all resources
stockApi.List

// Filter all resources by their label using filters
stockApi.FilterByLabel [ ("locations.stockr.io/ismobile", Eq "false")]

// get a specific resource by its name
stockApi.Get "test"
