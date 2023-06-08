#load "../Stockr/Controller.fs"

open System.Net.Http
open controller
open System
open System.Text.Json

type TestSpec = { A: string }



let client = new HttpClient()
client.BaseAddress <- new Uri("https://localhost:7243/")

let handler (str: Event<TestSpec>) =
    async {
        printfn "RECV: %A" str
    }

async {
    let! cts = Async.CancellationToken
    let uri = "stocks.stockr.io/v1alpha1/stock/"
    let! result = watchResource<TestSpec> client uri handler cts
    return result
}
|> Async.RunSynchronously
|> printfn "%A"
