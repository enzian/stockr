module persistence

open MongoDB.Driver
open System

open stock
open locations
open FsHttp

open System.Text.Json
open System.Net

let Open (connStr: string) db =
    let client = new MongoClient(connStr)
    client.GetDatabase(db)

let encodeKey (s: string) =
    s.Replace("\\", "\\\\").Replace("$", "\\u0024").Replace(".", "\\u002e")

let decodeKey (s: string) =
    s.Replace("\\\\", "\\").Replace("\\u0024", "$").Replace("\\u002e", ".")

let toMap (dic: System.Collections.Generic.IDictionary<_, _>) =
    dic |> Seq.map (fun x -> (x.Key |> encodeKey, x.Value) ) |> Map.ofSeq
let fromDict (dic: System.Collections.Generic.IDictionary<_, _>) =
    dic |> Seq.map (fun x -> (x.Key |> decodeKey, x.Value) ) |> Map.ofSeq

type StockModel(id: string, location, material, quantity, unit, labels, annotations) =
    member this.Id = id
    member this.Location = location
    member this.Material = material
    member this.Quantity = quantity
    member this.Unit = unit
    member this.Labels = labels
    member this.Annotations = annotations

type EtcdKv = { key: string; value: string}
type EtcdKvRange = { kvs: EtcdKv array option ; count : string option }


let CreateStockRecord host s =
    try
        let (amount, unit) = s.Amount

        let stockModel = new StockModel(s.Id, s.Location, s.Material.Value, amount.Value, unit.Value, s.Labels, s.Annotations)
        
        let resp =
            http {
                POST (sprintf "%skv/put" host)
                body
                jsonSerialize {|
                    key = sprintf "/locations/%s" s.Id 
                        |> System.Text.Encoding.UTF8.GetBytes
                        |> Convert.ToBase64String
                    value = JsonSerializer.Serialize stockModel 
                        |> System.Text.Encoding.UTF8.GetBytes 
                        |> Convert.ToBase64String
                |}
            }
            |> Request.send
        
        match resp.statusCode with
            | HttpStatusCode.OK -> Ok ()
            | status -> Error (sprintf "Request to ETCD failed: %O" status)
        
    with ex ->
        Error ex.Message

let DeleteStockRecord host (s: string) =
        let resp =
            http {
                POST (sprintf "%skv/deleterange" host)
                body
                jsonSerialize {|
                    key = sprintf "/locations/%s" s 
                        |> System.Text.Encoding.UTF8.GetBytes
                        |> Convert.ToBase64String
                |}
            }
            |> Request.send
        match resp.statusCode with
            | HttpStatusCode.OK -> Ok ()
            | status -> Error (sprintf "Request to ETCD failed: %O" status)

let UpdateStockRecord = CreateStockRecord

let FindStockRecordById host s =
    sprintf "/locations/%s" s 
        |> System.Text.Encoding.UTF8.GetBytes
        |> Convert.ToBase64String
        |> printfn "%s"
    try
        let resp =
            http {
                POST (sprintf "%skv/range" host)
                body
                jsonSerialize {|
                    key = sprintf "/locations/%s" s 
                        |> System.Text.Encoding.UTF8.GetBytes
                        |> Convert.ToBase64String
                |}
            }
            |> Request.send
            |> Response.assert2xx
            |> Response.toJson
            |> JsonSerializer.Deserialize<EtcdKvRange>
        match (resp.count, resp.kvs) with
        | (Some c, Some kvs) when (c |> int) > 0 ->
            let x = kvs[0] |> (fun x -> x.value) |> Convert.FromBase64String |> JsonSerializer.Deserialize<StockModel>
            
            Ok (Some {
                Id = x.Id
                Location = x.Location
                Material = x.Material |> Material
                Amount = (x.Quantity |> Quantity, x.Unit |> Unit)
                Labels = x.Labels
                Annotations = x.Annotations })
        | _  -> Ok None
    with ex ->
        Error ex.Message

let FindStockByLocation host location : Result<seq<stock.Stock>, string> =
    // try
    //     let filter = Builders<StockModel>.Filter.Eq ((fun x -> x.Location), location)

    //     Ok(
    //         col.Find(filter).ToList() :> seq<StockModel>
    //         |> Seq.map (fun x ->
    //             { Id = x.Id
    //               Location = x.Location
    //               Material = x.Material |> Material
    //               Amount = (x.Quantity |> Quantity, x.Unit |> Unit)
    //               Labels = x.Labels |> toMap
    //               Annotations = x.Annotations |> toMap })
    //     )
    // with ex ->
    //     Error ex.Message
    Ok ([] :> seq<stock.Stock>)

type StockRepository =
    { Create: stock.Stock -> Result<unit, string>
      Delete: string -> Result<unit, string>
      Update: stock.Stock -> Result<unit, string>
      FindById: string -> Result<Option<stock.Stock>, string>
      FindByLocation: string -> Result<seq<stock.Stock>, string> }

let StockRepo host =
    { Create = CreateStockRecord host 
      Delete = DeleteStockRecord host
      Update = UpdateStockRecord host
      FindById = FindStockRecordById host
      FindByLocation = FindStockByLocation host }

type LocationModel(id, labels, annotations) =
    member this.Id = id
    member this.Labels = labels
    member this.Annotations = annotations

let CreateLocation (col: IMongoCollection<LocationModel>) s =
    try
        col.InsertOne(new LocationModel(s.Id, s.Labels |> toMap |> Map.toSeq |> dict, s.Annotations |> toMap |> Map.toSeq |> dict))
        |> ignore

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

        let model =
            new LocationModel(s.Id, s.Labels |> toMap |> Map.toSeq |> dict, s.Annotations |> toMap |> Map.toSeq |> dict)

        col.ReplaceOne(filter, model) |> ignore
        Ok()
    with ex ->
        Error ex.Message


let FindLocationById (col: IMongoCollection<LocationModel>) s =
    try
        let filter = Builders<LocationModel>.Filter.Eq ((fun x -> x.Id), s)
        let result = col.Find(filter).Limit(1).FirstOrDefault()

        Ok(
            Some(
                { Id = result.Id
                  Labels = result.Labels |> fromDict
                  Annotations = result.Annotations |> fromDict }
            )
        )
    with
    | :? NullReferenceException -> Ok(None)
    | ex -> Error ex.Message

type Is = 
    | In of seq<string>
    | NotIn of seq<string>
    | Eq of string
    | NotEq of string
    | Set
    | NotSet

type KeyIs = (string * Is)

let findByLabel (col: IMongoCollection<LocationModel>) (labelQuery : KeyIs) =
    try
        let filter =
            match labelQuery with
            | (k, Eq v ) ->  Builders<LocationModel>.Filter.Eq ((sprintf "Labels.%s" (encodeKey k)), v)
            | (k, NotEq v ) ->  Builders<LocationModel>.Filter.Not (
                    Builders<LocationModel>.Filter.Eq ((sprintf "Labels.%s" k), (encodeKey k)))
            | (k, Set) -> Builders<LocationModel>.Filter.Exists (sprintf "Labels.%s" (encodeKey k))
            | (k, NotSet) -> Builders<LocationModel>.Filter.Not (
                Builders<LocationModel>.Filter.Exists (sprintf "Labels.%s" (encodeKey k)))
            | (k, In values) -> Builders<LocationModel>.Filter.In ((sprintf "Labels.%s" (encodeKey k)), values)
            | (k, NotIn values) -> Builders<LocationModel>.Filter.Not (
                Builders<LocationModel>.Filter.In ((sprintf "Labels.%s" (encodeKey k)), values))

        let results = col.Find(filter).ToList()

        Ok(
            Some( [for result in results ->
                    { Id = result.Id
                      Labels = result.Labels |> fromDict
                      Annotations = result.Annotations |> fromDict }
                ]
            )
        )
    with
    | :? NullReferenceException -> Ok(None)
    | ex -> Error ex.Message

type LocationRepository =
    { Create: locations.Location -> Result<unit, string>
      Delete: string -> Result<unit, string>
      Update: locations.Location -> Result<unit, string>
      FindById: string -> Result<Option<locations.Location>, string>
      FindByLabel: KeyIs -> Result<Option<locations.Location list>, string> }

let LocationRepo (col: IMongoCollection<LocationModel>) =
    { Create = CreateLocation col
      Delete = DeleteLocation col
      Update = UpdateLocation col
      FindById = FindLocationById col
      FindByLabel = findByLabel col }
    

