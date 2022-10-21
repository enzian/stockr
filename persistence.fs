module persistence

open MongoDB.Driver
open MongoDB.Bson
open System

open stock
open locations

let Open (connStr: string) db =
    let client = new MongoClient(connStr)
    client.GetDatabase(db)

type StockModel(id: string, location, material, quantity, unit) =
    member this.Id = id
    member this.Location = location
    member this.Material = material
    member this.Quantity = quantity
    member this.Unit = unit

let CreateStockRecord (col: IMongoCollection<StockModel>) s =
    try
        let (amount, unit) = s.Amount

        col.InsertOne(new StockModel(s.Id, s.Location, s.Material.Value, amount.Value, unit.Value))
        |> ignore

        Ok()
    with ex ->
        Error ex.Message

let DeleteStockRecord (col: IMongoCollection<StockModel>) (s: string) =
    try
        col.DeleteOne(fun x -> x.Id = s) |> ignore
        Ok()
    with ex ->
        Error ex.Message

let UpdateStockRecord (col: IMongoCollection<StockModel>) s =
    try
        let (amount, unit) = s.Amount
        let filter = Builders<StockModel>.Filter.Eq ((fun x -> x.Id), s.Id)

        let model =
            new StockModel(s.Id, s.Location, s.Material.Value, amount.Value, unit.Value)

        col.ReplaceOne(filter, model)
        Ok()
    with ex ->
        Error ex.Message


let FindStockRecordById (col: IMongoCollection<StockModel>) s =
    try
        let filter = Builders<StockModel>.Filter.Eq ((fun x -> x.Id), s)
        let result = col.Find(filter).Limit(1).FirstOrDefault()

        Ok(
            Some(
                { Id = result.Id
                  Location = result.Location
                  Material = result.Material |> Material
                  Amount = (result.Quantity |> Quantity, result.Unit |> Unit) }
            )
        )
    with
    | :? NullReferenceException -> Ok(None)
    | ex -> Error ex.Message

let FindStockByLocation (col: IMongoCollection<StockModel>) location =
    try
        let filter = Builders<StockModel>.Filter.Eq ((fun x -> x.Location), location)
        Ok (
            col.Find(filter).ToList()
            :> seq<StockModel>
            |> Seq.map (fun x ->
                { Id = x.Id
                  Location = x.Location
                  Material = x.Material |> Material
                  Amount = (x.Quantity |> Quantity, x.Unit |> Unit) } )
        )
    with ex -> Error ex.Message

type StockRepository =
    { Create: stock.Stock -> Result<unit, string>
      Delete: string -> Result<unit, string>
      Update: stock.Stock -> Result<unit, string>
      FindById: string -> Result<Option<stock.Stock>, string>
      FindByLocation: string -> Result<seq<stock.Stock>, string> }

let StockRepo (col: IMongoCollection<StockModel>) =
    { Create = CreateStockRecord col
      Delete = DeleteStockRecord col
      Update = UpdateStockRecord col
      FindById = FindStockRecordById col 
      FindByLocation = FindStockByLocation col }

type LocationModel(id, labels, annotations) =
    member this.Id = id
    member this.Labels = labels
    member this.Annotations = annotations

let CreateLocation (col: IMongoCollection<LocationModel>) s =
    try
        col.InsertOne(new LocationModel(s.Id, s.Labels |> Map.toSeq |> dict, s.Annotations |> Map.toSeq |> dict)) |> ignore
        Ok()
    with ex ->
        Error ex.Message

let DeleteLocation (col: IMongoCollection<LocationModel>) (s: string) =
    try
        col.DeleteOne(fun x -> x.Id = s) |> ignore
        Ok()
    with ex ->
        Error ex.Message

let UpdateLocation (col: IMongoCollection<LocationModel>) s =
    try
        let filter = Builders<LocationModel>.Filter.Eq ((fun x -> x.Id), s.Id)
        let model = new LocationModel(s.Id, s.Labels |> Map.toSeq |> dict, s.Annotations |> Map.toSeq |> dict)

        col.ReplaceOne(filter, model)
        Ok()
    with ex ->
        Error ex.Message

let toMap (dic : System.Collections.Generic.IDictionary<_,_>) = 
    dic 
    |> Seq.map (|KeyValue|)  
    |> Map.ofSeq


let FindLocationById (col: IMongoCollection<LocationModel>) s =
    try
        let filter = Builders<LocationModel>.Filter.Eq ((fun x -> x.Id), s)
        let result = col.Find(filter).Limit(1).FirstOrDefault()

        Ok(
            Some(
                { Id = result.Id
                  Labels = result.Labels |> toMap
                  Annotations = result.Annotations |> toMap }
            )
        )
    with
    | :? NullReferenceException -> Ok(None)
    | ex -> Error ex.Message

type LocationRepository =
    { Create: locations.Location -> Result<unit, string>
      Delete: string -> Result<unit, string>
      Update: locations.Location -> Result<unit, string>
      FindById: string -> Result<Option<locations.Location>, string> }

let LocationRepo (col: IMongoCollection<LocationModel>) =
    { Create = CreateLocation col
      Delete = DeleteLocation col
      Update = UpdateLocation col
      FindById = FindLocationById col }
