open System.Net.Http
open System
open System.Threading
open stock
open api

type StockManifest = 
    { spec: ApiStock
      metadata: Metadata }
    interface Manifest with 
        member this.metadata = this.metadata 

let client= new HttpClient()
client.BaseAddress <- new Uri("https://localhost:7243/apis/")

[<EntryPoint>]
let main argv = 

    let stockApi = 
        ManifestsFor<StockManifest> 
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