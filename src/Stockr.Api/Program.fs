open Giraffe
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.AspNetCore.Http
open dotnet_etcd
open dotnet_etcd.interfaces
open System.IO
open System.Text.Json
open System.Text.Json.Nodes

type Metadata = {
    name: string
    labels: Map<string, string>
    annotations: Map<string, string>
}

type SpecObject = {
    group: string
    version: string
    kind: string
    metadata: Metadata
    spec: JsonElement
}

type Stock = {
    id: string
}

// let serializerbuilder= new SerializerBuilder()
// let serializer = serializerbuilder.WithNamingConvention(CamelCaseNamingConvention.Instance).Build()

// let yaml (o : obj) : HttpHandler =
//     fun (next : HttpFunc) (ctx : HttpContext) ->
//         let content = serializer.Serialize(o)
//         ctx.Response.BodyWriter.WriteAsync(Encoding.UTF8.GetBytes(content)) |> ignore
//         next ctx

// type CustomNegotiationConfig (baseConfig : INegotiationConfig) =
//     let plainText x = text (x.ToString())

//     interface INegotiationConfig with

//         member __.UnacceptableHandler =
//             baseConfig.UnacceptableHandler

//         member __.Rules =
//                 dict [
//                     "*/*"             , json
//                     "application/json", json
//                     "application/xml" , xml
//                     "text/xml"        , xml
//                     "application/yaml", yaml
//                     "text/plain"      , plainText
//                 ]

let kindPrefix group version kind = 
    match (group, version, kind) with 
    | ("locations.stockr.io","v1alpha1","location") -> "/locations/"
    | ("stocks.stockr.io","v1alpha1","stock") -> "/stocks/"
    | _ -> ""

// let deserializeKind group version kind = 
//     match (group, version, kind) with 
//     | ("locations.stockr.io","v1alpha1","location") -> 
//         (fun (s : string) -> JsonSerializer.Deserialize<Stock> s)
//     | ("stocks.stockr.io","v1alpha1","stock") -> JsonSerializer.Deserialize<Stock>
//     | _ -> ()

let listHandler(group: string, version: string, kind: string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) -> 
        task {
            let client = ctx.GetService<IEtcdClient>()
            let key = Path.Combine("/registry/", (kindPrefix group version kind))
            let! kvs = client.GetRangeValAsync(key)
            let obj = kvs.Values |> Seq.map JsonSerializer.Deserialize<SpecObject>
            return! Successful.OK obj next ctx
        }


let PutManifest(group: string, version: string, kind: string, name: string) : HttpHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let! o = ctx.BindJsonAsync<SpecObject>()
            let o = { o with group = group; version = version; kind = kind }
            let value =  o |> JsonSerializer.Serialize
            let client = ctx.GetService<IEtcdClient>()
            let key = Path.Combine("/registry/", (kindPrefix group version kind), name)
            client.PutAsync(key,value) |> Async.AwaitTask |> ignore
            return! Successful.OK o next ctx
        }

let webApp =
    choose [
        GET >=> choose [
            routef "/apis/%s/%s/%s" listHandler
        ]
        PUT >=> choose [
            routef "/apis/%s/%s/%s/%s" PutManifest
        ]
        RequestErrors.NOT_FOUND "Not Found"
    ]

type Startup() =
    member __.ConfigureServices (services : IServiceCollection) =
        services.AddGiraffe() |> ignore
        services.AddSingleton<IEtcdClient>(new EtcdClient("https://localhost:2379"))

    member __.Configure (app : IApplicationBuilder)
                        (env : IHostingEnvironment)
                        (loggerFactory : ILoggerFactory) =
        // Add Giraffe to the ASP.NET Core pipeline
        app.UseGiraffe webApp

let configureApp (app : IApplicationBuilder) =
    // Add Giraffe to the ASP.NET Core pipeline
    app.UseGiraffe webApp |> ignore
    // app.AddSingleton<IEtcdClient>(new EtcdClient("https://localhost:2379"))

let configureServices (services : IServiceCollection) =
    // Add Giraffe dependencies
    services.AddGiraffe() |> ignore
    services.AddSingleton<IEtcdClient>(new EtcdClient("https://localhost:2379")) |> ignore



[<EntryPoint>]
let main _ =
    Host.CreateDefaultBuilder()
        .ConfigureWebHostDefaults(
            fun webHostBuilder ->
                webHostBuilder
                    .Configure(configureApp)
                    .ConfigureServices(configureServices)
                    |> ignore)
        .Build()
        .Run()
    0

