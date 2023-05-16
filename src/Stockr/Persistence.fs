module persistence

open System

open System.Text.Json
open dotnet_etcd

type Metadata = {
    labels: Map<string, string>
    annotations: Map<string, string> 
}

type SpecType<'T> = {
    name: string
    metadata: Metadata
    spec: 'T
}

type Is = 
    | In of seq<string>
    | NotIn of seq<string>
    | Eq of string
    | NotEq of string
    | Set
    | NotSet

type KeyIs = (string * Is)

type Repository<'T> =
    { Create: SpecType<'T> -> Result<unit, string>
      Delete: string -> Result<unit, string>
      Update: SpecType<'T> -> Result<unit, string>
      FindById: string -> Result<Option<SpecType<'T>>, string>
      FindByLabel: KeyIs -> Result<SpecType<'T> seq, string> 
      List: Result<SpecType<'T> seq, string> }

let inline Create<'T> (client: EtcdClient) ns (s:SpecType<'T>) =
    try
        let key = sprintf "%s%A" ns s.name
        client.Put(key, JsonSerializer.Serialize(s)) |> ignore
        Ok ()
    with ex ->
        Error ex.Message

let Update<'T> = Create<'T>

let FindById<'T> (client: EtcdClient) ns s =
    try
        let key = sprintf "%s%s" ns s
        let range = client.Get(key)
        if range.Count > 0 then
            let x = range.Kvs[0].Value.ToStringUtf8() |> JsonSerializer.Deserialize<SpecType<'T>>
            Ok (Some x)
        else 
            Ok None
    with ex ->
        Error ex.Message


let FindByLabel<'T> (client: EtcdClient) ns (labelQuery : KeyIs) : Result<SpecType<'T> seq, string> =
    try
        let range = client.GetRange(ns)

        if range.Count > 0 then
            let stocks = 
                range.Kvs
                |> Seq.map (fun x -> x.Value.ToStringUtf8())
                |> Seq.map JsonSerializer.Deserialize<SpecType<'T>>
                |> Seq.filter (fun x ->
                    let labels = x.metadata.labels
                    match labelQuery with
                    | (k, Eq v ) -> labels.ContainsKey(k) && labels.Item(k) = v
                    | (k, NotEq v ) -> labels.ContainsKey(k) && labels.Item(k) <> v
                    | (k, Set) -> labels.ContainsKey(k)
                    | (k, NotSet) -> not (labels.ContainsKey(k))
                    | (k, In values) -> labels.ContainsKey(k) && values |> Seq.contains (labels.Item(k))
                    | (k, NotIn values) -> labels.ContainsKey(k) && values |> Seq.contains(labels.Item(k)) |> not
                    )
            Ok stocks
        else 
            Ok [||]
    with ex ->
        Error ex.Message

let List<'T> (client: EtcdClient) ns : Result<SpecType<'T> seq, string> =
    try
        let range = client.GetRange(ns)

        if range.Count > 0 then
            let stocks = 
                range.Kvs
                |> Seq.map (fun x -> x.Value.ToStringUtf8())
                |> Seq.map JsonSerializer.Deserialize<SpecType<'T>>
            Ok stocks
        else 
            Ok [||]
    with ex ->
        Error ex.Message

let Delete (client: EtcdClient) ns name =
    try
        let key = sprintf "%s%s" ns name
        client.Delete(key) |> ignore
        Ok ()
    with ex ->
        Error ex.Message

let inline newRepository<'T> (etcdClient: EtcdClient) ns = 
    ({
        Create = Create<'T> etcdClient ns
        Delete = Delete etcdClient ns
        Update = Update<'T> etcdClient ns
        FindById = FindById<'T> etcdClient ns
        FindByLabel = FindByLabel<'T> etcdClient ns
        List = List<'T> etcdClient ns } : Repository<'T>)


// type StockModel(id: string, location, material, quantity, unit, labels, annotations) =
//     member this.Id = id
//     member this.Location = location
//     member this.Material = material
//     member this.Quantity = quantity
//     member this.Unit = unit
//     member this.Labels = labels
//     member this.Annotations = annotations

// type EtcdKv = { key: string; value: string}
// type EtcdKvRange = { kvs: EtcdKv array option ; count : string option }

// let CreateStockRecord (client: EtcdClient) s =
//     try
//         let (amount, unit) = s.Amount

//         let stockModel = new StockModel(s.Id, s.Location, s.Material.Value, amount.Value, unit.Value, s.Labels, s.Annotations)
//         let key = sprintf "/stocks/%s" stockModel.Id
//         client.Put(key, JsonSerializer.Serialize(stockModel)) |> ignore
//         Ok ()
//     with ex ->
//         Error ex.Message

// let DeleteStockRecord (client: EtcdClient) (s: string) =
//     try
//         let key = sprintf "/stocks/%s" s
//         client.Delete(key) |> ignore
//         Ok ()
//     with ex ->
//         Error ex.Message

// let UpdateStockRecord = CreateStockRecord

// let FindStockRecordById (client: EtcdClient) s =
//     try
//         let key = sprintf "/stocks/%s" s
//         let range = client.Get(key)
//         if range.Count > 0 then
//             let x = range.Kvs[0].Value.ToStringUtf8() |> JsonSerializer.Deserialize<StockModel>
//             Ok (Some {
//                     Id = x.Id
//                     Location = x.Location
//                     Material = x.Material |> Material
//                     Amount = (x.Quantity |> Quantity, x.Unit |> Unit)
//                     Labels = x.Labels
//                     Annotations = x.Annotations })
//         else 
//             Ok None
//     with ex ->
//         Error ex.Message

// let FindStockByLocation (client: EtcdClient) location =
//     try
//         let range = client.GetRange("/stocks/")
//         let locations = 
//             range.Kvs
//             |> Seq.map (fun x -> x.Value.ToStringUtf8())
//             |> Seq.map JsonSerializer.Deserialize<StockModel>
//             |> Seq.filter (fun x -> x.Location = location)
//             |> Seq.map (fun x -> {
//                     Id = x.Id
//                     Location = x.Location
//                     Material = x.Material |> Material
//                     Amount = (x.Quantity |> Quantity, x.Unit |> Unit)
//                     Labels = x.Labels
//                     Annotations = x.Annotations })
//         Ok locations

//     with ex ->
//         Error ex.Message
    
// type StockRepository =
//     { Create: stock.Stock -> Result<unit, string>
//       Delete: string -> Result<unit, string>
//       Update: stock.Stock -> Result<unit, string>
//       FindById: string -> Result<Option<stock.Stock>, string>
//       FindByLocation: string -> Result<seq<stock.Stock>, string> }

// let StockRepo (client: EtcdClient) =
//     { Create = CreateStockRecord client 
//       Delete = DeleteStockRecord client
//       Update = UpdateStockRecord client
//       FindById = FindStockRecordById client
//       FindByLocation = FindStockByLocation client }

// type LocationModel(id, labels, annotations) =
//     member this.Id = id
//     member this.Labels = labels
//     member this.Annotations = annotations


// let CreateLocation (client: EtcdClient) s =
//     try
//         let locationModel = new LocationModel(s.Id, s.Labels, s.Annotations)
//         let key = sprintf "/locations/%s" locationModel.Id
//         client.Put(key, locationModel |> JsonSerializer.Serialize) |> ignore
//         Ok ()
        
//     with ex ->
//         Error ex.Message

// let DeleteLocation (client: EtcdClient) (s: string) =
//     try
//         let key = sprintf "/locations/%s" s
//         client.Delete(key) |> ignore
//         Ok ()
//     with ex ->
//         Error ex.Message

// let UpdateLocation = CreateLocation

// let FindLocationById (client: EtcdClient) s =
//     try
//         let key = sprintf "/locations/%s" s
//         let range = client.Get(key)
//         if range.Count > 0 then
//             let x = range.Kvs[0].Value.ToStringUtf8() |> JsonSerializer.Deserialize<LocationModel>
//             Ok (Some {
//                     Id = x.Id
//                     Labels = x.Labels
//                     Annotations = x.Annotations })
//         else 
//             Ok None
//     with ex ->
//         Error ex.Message

// let findByLabel (client: EtcdClient) (labelQuery : KeyIs) : Result<Location seq, string> =
//     try
//         let range = client.GetRange("/locations/")

//         if range.Count > 0 then
//             let stocks = 
//                 range.Kvs
//                 |> Seq.map (fun x -> x.Value.ToStringUtf8())
//                 |> Seq.map JsonSerializer.Deserialize<LocationModel>
//                 |> Seq.filter (fun x ->
//                     match labelQuery with
//                     | (k, Eq v ) -> x.Labels.ContainsKey(k) && x.Labels.Item(k) = v
//                     | (k, NotEq v ) -> x.Labels.ContainsKey(k) && x.Labels.Item(k) <> v
//                     | (k, Set) -> x.Labels.ContainsKey(k)
//                     | (k, NotSet) -> not (x.Labels.ContainsKey(k))
//                     | (k, In values) -> x.Labels.ContainsKey(k) && values |> Seq.contains (x.Labels.Item(k))
//                     | (k, NotIn values) -> x.Labels.ContainsKey(k) && values |> Seq.contains(x.Labels.Item(k)) |> not
//                     )
//                 |> Seq.map (fun x ->
//                     {
//                     Id = x.Id
//                     Labels = x.Labels
//                     Annotations = x.Annotations
//                     })
//             Ok stocks
//         else 
//             Ok [||]
//     with ex ->
//         Error ex.Message

// type LocationRepository =
//     { Create: locations.Location -> Result<unit, string>
//       Delete: string -> Result<unit, string>
//       Update: locations.Location -> Result<unit, string>
//       FindById: string -> Result<Option<locations.Location>, string>
//       FindByLabel: KeyIs -> Result<locations.Location seq, string> }

// let LocationRepo (client: EtcdClient) =
//     { Create = CreateLocation client
//       Delete = DeleteLocation client
//       Update = UpdateLocation client
//       FindById = FindLocationById client
//       FindByLabel = findByLabel client }
    