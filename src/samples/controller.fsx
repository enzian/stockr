#load "../Stockr/Controller.fs"

open System.Net.Http
open System
open controller;

type TestSpec = { A: string }
type TestStatus = { B: string }



let client= new HttpClient()
client.BaseAddress <- new Uri("https://localhost:7243/")

let handler (str: Event<TestSpec, TestStatus>) =
    async {
        printfn "RECV: %A" str
    }

async {
    let! cts = Async.CancellationToken
    let uri = "stocks.stockr.io/v1alpha1/stock/"
    let! result = watchResource<TestSpec, TestStatus> client uri handler cts
    return result
}
|> Async.RunSynchronously
|> printfn "%A"
