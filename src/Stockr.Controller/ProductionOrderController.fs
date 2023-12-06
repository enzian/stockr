module production_order_controller
open System.Threading
open api

type ProductionLine = {
    material: string
    quantity: string
}

type ProductionOrder = {
    id : string
    bom: ProductionLine list
    material: string
    quantity: string
    from: string
    target: string
}

type ProductionOrderSpecManifest = 
    { spec: ProductionOrder
      metadata: Metadata }
    interface Manifest with 
        member this.metadata = this.metadata 

let runController (ct: CancellationToken) client =
    async {
        printfn "Starting ProductionOrderController"
        let api = ManifestsFor<ProductionOrderSpecManifest> client "logistics.stockr.io/v1alpha1/production-orders/"
        let (aggregate, watchObs) = utilities.watchResourceOfType api ct
        aggregate.Subscribe(fun x -> 
            printfn "ProductionOrderController has %i order on record" (x.Count)) |> ignore

        (Async.AwaitWaitHandle ct.WaitHandle) |> Async.RunSynchronously |> ignore
        printfn "Stopped ProductionOrderController"
    }
