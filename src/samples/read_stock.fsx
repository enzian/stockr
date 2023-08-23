#r "nuget: FsHttp, 11.0.0"

#load "../Stockr/Api.fs"

open System.Net.Http
open System
open api

type StockSpec = { material: string; qty : string }
type StockStatus = { B: string }

let client= new HttpClient()
client.BaseAddress <- new Uri("https://localhost:7243/apis/")

let stockApi = 
    api.ManifestsFor<StockSpec, StockStatus> 
        client
        "logistics.stockr.io/v1alpha1/stock"

stockApi.List

stockApi.Get "test"
