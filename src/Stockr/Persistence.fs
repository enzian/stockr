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
                    key = sprintf "/stocks/%s" s.Id 
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
                    key = sprintf "/stocks/%s" s 
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
    try
        let resp =
            http {
                POST (sprintf "%skv/range" host)
                body
                jsonSerialize {|
                    key = sprintf "/stocks/%s" s 
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
    let key = 
        "/stocks/"
        |> System.Text.Encoding.UTF8.GetBytes
    
    let rec incLast arr = 
        match arr with
        | [x] when x = 0xffuy -> [0x00uy]
        | [x] when x < 0xffuy -> [x + 1uy]
        | head::tail -> head::(incLast tail)

    try
        let resp =
            http {
                POST (sprintf "%skv/range" host)
                body
                jsonSerialize {|
                    key = key |> Convert.ToBase64String
                    range_end = key |> Array.toList |> incLast |> List.toArray |> Convert.ToBase64String
                |}
            }
            |> Request.send
            |> Response.assert2xx
            |> Response.toJson
            |> JsonSerializer.Deserialize<EtcdKvRange>
        
        match (resp.count, resp.kvs) with
        | (Some c, Some kvs) when (c |> int) > 0 ->
            let stocks = 
                kvs
                |> Array.map (fun x -> x.value |> Convert.FromBase64String |> JsonSerializer.Deserialize<StockModel>)
                |> Array.filter (fun x -> x.Location = location)
                |> Array.map (fun x ->
                    {
                    Id = x.Id
                    Location = x.Location
                    Material = x.Material |> Material
                    Amount = (x.Quantity |> Quantity, x.Unit |> Unit)
                    Labels = x.Labels
                    Annotations = x.Annotations
                    })
            Ok stocks
        | _  -> Ok []
    with ex ->
        Error ex.Message
    // Ok ([] :> seq<stock.Stock>)

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

let CreateLocation host s =
    try
        let locationModel = new LocationModel(s.Id, s.Labels, s.Annotations)
        
        let resp =
            http {
                POST (sprintf "%skv/put" host)
                body
                jsonSerialize {|
                    key = sprintf "/locations/%s" s.Id 
                        |> System.Text.Encoding.UTF8.GetBytes
                        |> Convert.ToBase64String
                    value = JsonSerializer.Serialize locationModel 
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

let DeleteLocation host (s: string) =
    try
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
    with ex ->
        Error ex.Message

let UpdateLocation = CreateLocation

let FindLocationById host s =
    let key = 
        "/locations/"
        |> System.Text.Encoding.UTF8.GetBytes
    
    let rec incLast arr = 
        match arr with
        | [x] when x = 0xffuy -> [0x00uy]
        | [x] when x < 0xffuy -> [x + 1uy]
        | head::tail -> head::(incLast tail)

    try
        let resp =
            http {
                POST (sprintf "%skv/range" host)
                body
                jsonSerialize {|
                    key = key |> Convert.ToBase64String
                    range_end = key |> Array.toList |> incLast |> List.toArray |> Convert.ToBase64String
                |}
            }
            |> Request.send
            |> Response.assert2xx
            |> Response.toJson
            |> JsonSerializer.Deserialize<EtcdKvRange>
        
        match (resp.count, resp.kvs) with
        | (Some c, Some kvs) when (c |> int) > 0 ->
            let stocks = 
                kvs
                |> Array.map (fun x -> x.value |> Convert.FromBase64String |> JsonSerializer.Deserialize<StockModel>)
                |> Array.filter (fun x -> x.Location = s)
                |> Array.map (fun x ->
                    {
                    Id = x.Id
                    Labels = x.Labels
                    Annotations = x.Annotations
                    })
            Ok (Some (stocks |> Array.head))
        | _  -> Ok None
    with ex ->
        Error ex.Message

type Is = 
    | In of seq<string>
    | NotIn of seq<string>
    | Eq of string
    | NotEq of string
    | Set
    | NotSet

type KeyIs = (string * Is)

let findByLabel host (labelQuery : KeyIs) : Result<Location array, string> =
    let key = 
        "/locations/"
        |> System.Text.Encoding.UTF8.GetBytes
    
    let rec incLast arr = 
        match arr with
        | [x] when x = 0xffuy -> [0x00uy]
        | [x] when x < 0xffuy -> [x + 1uy]
        | head::tail -> head::(incLast tail)

    try
        let resp =
            http {
                POST (sprintf "%skv/range" host)
                body
                jsonSerialize {|
                    key = key |> Convert.ToBase64String
                    range_end = key |> Array.toList |> incLast |> List.toArray |> Convert.ToBase64String
                |}
            }
            |> Request.send
            |> Response.assert2xx
            |> Response.toJson
            |> JsonSerializer.Deserialize<EtcdKvRange>
        
        match (resp.count, resp.kvs) with
        | (Some c, Some kvs) when (c |> int) > 0 ->
            let stocks = 
                kvs
                |> Array.map (fun x -> x.value |> Convert.FromBase64String |> JsonSerializer.Deserialize<LocationModel>)
                |> Array.filter (fun x ->
                    match labelQuery with
                    | (k, Eq v ) -> x.Labels.ContainsKey(k) && x.Labels.Item(k) = v
                    | (k, NotEq v ) -> x.Labels.ContainsKey(k) && x.Labels.Item(k) <> v
                    | (k, Set) -> x.Labels.ContainsKey(k)
                    | (k, NotSet) -> not (x.Labels.ContainsKey(k))
                    | (k, In values) -> x.Labels.ContainsKey(k) && values |> Seq.contains (x.Labels.Item(k))
                    | (k, NotIn values) -> x.Labels.ContainsKey(k) && values |> Seq.contains(x.Labels.Item(k)) |> not
                    )
                |> Array.map (fun x ->
                    {
                    Id = x.Id
                    Labels = x.Labels
                    Annotations = x.Annotations
                    })
            Ok stocks
        | _  -> Ok [||]
    with ex ->
        Error ex.Message

type LocationRepository =
    { Create: locations.Location -> Result<unit, string>
      Delete: string -> Result<unit, string>
      Update: locations.Location -> Result<unit, string>
      FindById: string -> Result<Option<locations.Location>, string>
      FindByLabel: KeyIs -> Result<locations.Location array, string> }

let LocationRepo col =
    { Create = CreateLocation col
      Delete = DeleteLocation col
      Update = UpdateLocation col
      FindById = FindLocationById col
      FindByLabel = findByLabel col }
    

