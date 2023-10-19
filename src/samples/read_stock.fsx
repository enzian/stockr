#r "nuget: FsHttp, 11.0.0"

#load "../Stockr/Api.fs"
#load "../Stockr/Filters.fs"

open System.Net.Http
open System
open filter

type StockSpec = { material: string; qty : string }
type StockStatus = { B: string }

let client= new HttpClient()
client.BaseAddress <- new Uri("https://localhost:7243/apis/")

let stockApi = 
    api.ManifestsFor<StockSpec, StockStatus> 
        client
        "logistics.stockr.io/v1alpha1/stock"

// List all resources
stockApi.List

// Filter all resources by their label using filters
stockApi.FilterByLabel [ ("locations.stockr.io/ismobile", Eq "true")]

// get a specific resource by its name
stockApi.Get "test"
