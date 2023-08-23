#load "../Stockr/Controller.fs"

open System.Net.Http
open System
open controller;

type StockSpec = { material: string; qty : string }
type StockStatus = { B: string }

let client= new HttpClient()
client.BaseAddress <- new Uri("https://localhost:7243/")

async {
    let! cts = Async.CancellationToken
    let uri = "logistics.stockr.io/v1alpha1/stock"
    let! result = watchResource<StockSpec, StockStatus> client uri cts
    result.Subscribe((fun x -> printfn"%A"  x)) |> ignore
}
|> Async.RunSynchronously
