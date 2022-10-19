module persistence

open MongoDB.Driver
open MongoDB.Bson

open stock

let Open (connStr : string) db = 
    let client = new MongoClient(connStr)
    client.GetDatabase(db)

type StockModel(id, location, material, quantity, unit) = 
    member this.Id = id
    member this.Location = location
    member this.Material = material
    member this.Quantity = quantity
    member this.Unit = unit

type CreateStock = stock.Stock -> Result<unit, string>

type StockRepository = {
    Create: CreateStock
}

let StockRepo (col : IMongoCollection<StockModel>) = {
    Create = fun s -> 
        try
            let (amount, unit) = s.Amount
            col.InsertOne(
                new StockModel ( s.Id, s.Location, s.Material.Value, amount.Value, unit.Value))
            Ok ()
        with
            ex -> Error ex.Message
}