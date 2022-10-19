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

// [<CLIMutable>]
// type StockModel = {
//     Id: string
//     Location: string
//     Material: string
//     Quantity: int
//     Unit: string
// }

type CreateStock = stock.Stock -> bool

type StockRepository = {
    Create: CreateStock
}

let CreateRepo (db : IMongoDatabase) = {
    Create = fun s -> 
        try
            let (amount, unit) = s.Amount
            db.GetCollection<StockModel>("stocks").InsertOne(
                new StockModel ( s.Id, s.Location, s.Material.Value, amount.Value, unit.Value))
            true
        with
            ex -> 
                printf "Failed %s" ex.Message
                false
}