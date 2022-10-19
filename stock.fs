module stock

type Unit = Unit of string
    with
        member x.Value = match x with 
            | Unit s -> s

type Material = Material of string
    with
        member x.Value = match x with 
            | Material s -> s

type Quantity = Quantity of int
    with
        member x.Value = match x with 
            | Quantity s -> s

type Amount = Quantity * Unit

type Stock = {
    Id: string
    Location: string
    Material: Material
    Amount: Amount
}

type CreateStock = Stock -> bool