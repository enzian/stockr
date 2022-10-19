module persistence

open MongoDB.Driver
open MongoDB.Bson

open stock

let Open (connStr : string) db = 
    let client = new MongoClient(connStr)
    client.GetDatabase(db)

type StockModel(id: string, location, material, quantity, unit) = 
    member this.Id = id
    member this.Location = location
    member this.Material = material
    member this.Quantity = quantity
    member this.Unit = unit

let CreateStockRecord (col : IMongoCollection<StockModel>) s = 
        try
            let (amount, unit) = s.Amount
            col.InsertOne(
                new StockModel ( s.Id, s.Location, s.Material.Value, amount.Value, unit.Value))
            Ok ()
        with
            ex -> Error ex.Message

let DeleteStockRecord (col : IMongoCollection<StockModel>) (s : string) = 
        try
            col.DeleteOne(fun x -> x.Id = s) |> ignore
            Ok ()
        with
            ex -> Error ex.Message
let UpdateStockRecord (col : IMongoCollection<StockModel>) s = 
        try
            let (amount, unit) = s.Amount
            let filter = Builders<StockModel>.Filter.Eq((fun x -> x.Id), s.Id)
            let model = new StockModel ( s.Id, s.Location, s.Material.Value, amount.Value, unit.Value )
            col.ReplaceOne(filter, model)
            Ok ()
        with
            ex -> Error ex.Message

type StockRepository = {
    Create: stock.Stock -> Result<unit, string>
    Delete: string -> Result<unit, string>
    Update: stock.Stock -> Result<unit, string>
}

let StockRepo (col : IMongoCollection<StockModel>) = {
    Create = CreateStockRecord col 
    Delete = DeleteStockRecord col 
    Update = UpdateStockRecord col 
}

