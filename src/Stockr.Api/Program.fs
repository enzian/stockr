namespace Stockr.Api

#nowarn "20"

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open dotnet_etcd
open dotnet_etcd.interfaces

open Manifesto.AspNet


module Program =
    open dotnet_etcd.interfaces
    let exitCode = 0

    [<EntryPoint>]
    let main args =

        let builder = WebApplication.CreateBuilder(args)
        builder.Services |> hosting.configureServices
        builder.Services.AddSingleton<IEtcdClient>(new EtcdClient("http://localhost:2379"))
        let app = builder.Build()

        let knownKeyspaces group version ``type`` =
            match group, version, ``type`` with
            | "stocks.stockr.io", "v1alpha1", t when [ "stock"; "stocks"; "s" ] |> Seq.contains t ->
                "/registry/stocks.stockr.io/v1alpha1/stocks"
            | "logistics.stockr.io", "v1alpha1", t when [ "production-orders"; "po"; "production-order" ] |> Seq.contains t ->
                "/registry/logistics.stockr.io/v1alpha1/productionorders"
            | "logistics.stockr.io", "v1alpha1", t when [ "transport"; "transports"; "tr" ] |> Seq.contains t ->
                "/registry/logistics.stockr.io/v1alpha1/transports"
            | "logistics.stockr.io", "v1alpha1", t when [ "locations"; "lo"; "location" ] |> Seq.contains t ->
                "/registry/logistics.stockr.io/v1alpha1/locations"
            | "events.stockr.io", "v1", t when [ "event"; "events" ] |> Seq.contains t ->
                "/registry/events.stockr.io/v1/events"
            | _ -> null

        let resourceTTL group version kind =
            match group, version, kind with
            | "events.stockr.io", "v1", t when [ "event"; "events" ] |> Seq.contains t -> Some(60L * 60L * 3L)
            | _ -> None

        app
        |> hosting.configureApp (api.v1.controllers.endpoints knownKeyspaces resourceTTL)

        app.Run()
        exitCode
