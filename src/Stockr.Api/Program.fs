namespace Stockr.Api

#nowarn "20"

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open dotnet_etcd;
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
            | "stocks.stockr.io", "v1alpha1", t when [ "stock"; "stocks" ] |> Seq.contains t 
                -> "/registry/stocks.stockr.io/v1alpha1/stocks"
            | "logistics.stockr.io", "v1alpha1", t when [ "production-orders"; "po" ] |> Seq.contains t 
                -> "/registry/logistics.stockr.io/v1alpha1/productionorders"
            | "logistics.stockr.io", "v1alpha1", t when [ "transport"; "transports"; "tr" ] |> Seq.contains t 
                -> "/registry/logistics.stockr.io/v1alpha1/transports"
            | _ -> null
        let resourceTTL _ _ _ = None

        app |> hosting.configureApp (api.v1.controllers.endpoints knownKeyspaces resourceTTL)
        app.Run()
        exitCode
