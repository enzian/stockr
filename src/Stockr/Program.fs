open System.Net.Http
open System
open System.Threading

type TestSpec = { material: string }
type TestStatus = { B: string }

type StockSpec = { material: string; qty : string }
type StockStatus = { B: string }

let client= new HttpClient()
client.BaseAddress <- new Uri("https://localhost:7243/apis/")

[<EntryPoint>]
let main argv = 

    let stockApi = 
        api.ManifestsFor<StockSpec, StockStatus> 
            client
            "logistics.stockr.io/v1alpha1/stock"
    
    let cts = new CancellationTokenSource()

    async {
        let! ct = Async.CancellationToken
        let! result = stockApi.Watch ct
        let disposable = result.Subscribe((fun x -> printfn"%A"  x))
        
        Console.CancelKeyPress.Add(fun _ -> 
            printfn "interrupted..."
            disposable.Dispose()
            cts.Cancel())
        
        [|cts.Token.WaitHandle|]|> WaitHandle.WaitAll |> ignore
    }
    |> Async.RunSynchronously
    
    printfn "exiting"
    0