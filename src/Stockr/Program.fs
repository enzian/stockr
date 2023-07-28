open controller
open System.Net.Http
open System
open System.Threading

type TestSpec = { material: string }
type TestStatus = { B: string }

let handler (str: Event<TestSpec, TestStatus>) =
    async {
        printfn "RECV: %A" str
    }

[<EntryPoint>]
let main argv = 
    let client= new HttpClient()
    client.BaseAddress <- new Uri("https://localhost:7243/")
    
    let cts = new CancellationTokenSource()
    async {
        let! observable = watchResource<TestSpec, TestStatus> client "logistics.stockr.io/v1alpha1/stock" cts.Token 
        let disposable = observable |> Observable.subscribe (fun x -> printfn "received: %A" x)

        Console.CancelKeyPress.Add(fun _ -> 
            printfn "interrupted..."
            disposable.Dispose()
            cts.Cancel())
        
        [|cts.Token.WaitHandle|]|> WaitHandle.WaitAll |> ignore
    }
    |> Async.RunSynchronously
    
    printfn "exiting"
    0