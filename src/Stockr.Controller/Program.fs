open Argu
open System
open System.Threading
open System.Net.Http


type Arguments =
    | Controllers of controllers:string list

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Controllers _ -> "specify a list of controllers to run."


[<EntryPoint>]
let main argv = 
    let errorHandler = ProcessExiter(colorizer = function ErrorCode.HelpText -> None | _ -> Some ConsoleColor.Red)
    let parser = ArgumentParser.Create<Arguments>(programName = "stockr_controller", errorHandler = errorHandler)

    let parser = parser.ParseCommandLine argv
    let results = parser.GetResult (
        Controllers,
        defaultValue = ["ProductionOrderTransport";"StockLocation";"TransportOrderController"])

    let cts = new CancellationTokenSource()
    let token = cts.Token

    Console.CancelKeyPress.Add(fun _ -> 
        printfn "Stopping controllers..."
        cts.Cancel())

    let client = new HttpClient()
    client.BaseAddress <- new Uri("http://localhost:5000/apis/") 

    results
    |> Seq.map ( fun x -> 
            match x with
            | "ProductionOrderTransport" -> 
                production_order_controller.runController token client
            | "StockLocation" -> 
                async {
                    printfn "Starting StockLocation"
                    (Async.AwaitWaitHandle token.WaitHandle) |> Async.RunSynchronously |> ignore
                    printfn "Stopped StockLocation"
                }
            | "TransportOrderController" -> 
                transport_order_controller.runController token client
    )
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore

    0

    



