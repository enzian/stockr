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
