module production
open measurement

module api =
    open api

    type ProductionLine = {
        material: string
        quantity: string
    }

    type ProductionOrder = {
        bom: ProductionLine list
        material: string
        amount: string
        from: string
        target: string
    }

    type ProductionStatus = {
        state: string
    }

    type ProductionOrderSpecManifest = 
        { spec: ProductionOrder
          metadata: Metadata }
        interface Manifest with 
            member this.metadata = this.metadata 

    type ProductionOrderFullManifest = 
        { spec: ProductionOrder
          status: ProductionStatus option
          metadata: Metadata }
        interface Manifest with 
            member this.metadata = this.metadata 

    let Version = "v1alpha1"
    let apiGroup = "logistics.stockr.io"
    let apiKind = "production-order"

type ProductionLine = {
    material: string
    quantity: Quantity
}

type ProductionOrder = {
    bom: ProductionLine list
    material: string
    amount: Quantity
    from: string option
    target: string option
}

let toProductionOrder (apiSpec : api.ProductionOrder) = 
    {
        material = apiSpec.material
        amount = apiSpec.amount |> toQuantity
        from = if apiSpec.from = "" then None else Some apiSpec.from
        target = if apiSpec.from = "" then None else Some apiSpec.from
        bom = apiSpec.bom |> List.map (fun x -> { material = x.material; quantity = x.quantity |> toQuantity })
    }

let toProductionOrderSpec (po : ProductionOrder) : api.ProductionOrder = 
    {
        material = po.material
        amount = po.amount |> quantityToString
        from = match po.from with | Some x -> x | None -> ""
        target = match po.target with | Some x -> x | None -> ""
        bom = po.bom |> List.map (fun x -> { material = x.material; quantity = x.quantity |> quantityToString })
    }

type Status = 
    | Pending
    | InProgress
    | Completed
    | Failed
    | Unknow of string

let toStatus = function
    | "pending" -> Pending
    | "in-progress" -> InProgress
    | "completed" -> Completed
    | "failed" -> Failed
    | x -> Unknow x
let toString = function
    | Pending -> "pending"
    | InProgress -> "in-progress"
    | Completed -> "completed"
    | Failed -> "failed"
    | Unknow x -> x

type ProductionStatus = {
    state: Status
    reason: string option
}

let toProductionStatus (apiStatus : api.ProductionStatus) = 
    {
        state = apiStatus.state |> toStatus
        reason = None
    }

let toProductionStatusSpec (ps : ProductionStatus) : api.ProductionStatus = 
    {
        state = ps.state |> toString
    }
