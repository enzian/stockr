#r "nuget: FsHttp, 11.0.0"

#load "../Stockr/Api.fs"
#load "../Stockr/Controller.fs"

open System.Net.Http
open System

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

async {
    let! cts = Async.CancellationToken
    let! result = stockApi.Watch cts
    result.Subscribe((fun x -> printfn"%A"  x)) |> ignore
}
|> Async.RunSynchronously
