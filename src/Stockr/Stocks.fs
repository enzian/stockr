module stock

type Unit =
    | Unit of string

    member x.Value =
        match x with
        | Unit s -> s

type Material =
    | Material of string

    member x.Value =
        match x with
        | Material s -> s

type Quantity =
    | Quantity of int

    member x.Value =
        match x with
        | Quantity s -> s

    static member (+)(Quantity (left), Quantity (right)) = left + right |> Quantity
    static member (-)(Quantity (left), Quantity (right)) = left - right |> Quantity


type Amount = {
    qty: Quantity
    unit: Unit
}

type Stock = 
    {
      Location: string
      Material: Material
      Amount: Amount 
    }

type CreateStock = Stock -> bool
