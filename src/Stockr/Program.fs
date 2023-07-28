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
    Console.CancelKeyPress.Add(fun _ -> 
        printfn "interrupted..."
        cts.Cancel())


    async {
        let cts = cts.Token
        let uri = "logistics.stockr.io/v1alpha1/stock"
        let! result = watchResource<TestSpec, TestStatus> client uri handler cts
        return result
    }
    |> Async.RunSynchronously
    printfn "exiting"
    0