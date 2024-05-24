#r "nuget: Manifesto.Client.Fsharp"

#load "../Stockr.Controller/Location.fs"
#load "../Stockr.Controller/Stock.fs"
#load "../Stockr.Controller/Logistics.fs"

open System.Net.Http
open System
open stock
open filter
open System.Threading
open System.Net

HttpClient.DefaultProxy <- new WebProxy()
let client= new HttpClient()
client.BaseAddress <- new Uri("http://localhost:5000/apis/")

let stockApi = 
    api.ManifestsFor<StockSpecManifest> 
        client
        "stocks.stockr.io/v1alpha1/stock/"

// List all resources
stockApi.List CancellationToken.None 0 1000

// Filter all resources by their label using filters
stockApi.FilterByLabel 0 1000 [ ("locations.stockr.io/ismobile", Eq "false")]

// let FilterByLabel httpClient path limit continuation (keyIs: KeyIs seq) =
//     try
//         let filter = keyIs |> Seq.map formatLabelFilter |> String.concat ","
//         http {
//             config_transformHttpClient (fun _ -> httpClient)
//             GET(Path.Combine(httpClient.BaseAddress.ToString(), path))
//             query [
//                 ("filter", filter)
//                 ("limit", limit.ToString())
//                 ("continuation", continuation.ToString())]
//         }
//         |> Request.send
//         |> Response.deserializeJson<Page<StockSpecManifest>>
//     with e ->
//         eprintfn "Failed to load filtered manifests: %A" e
//         { items = Seq.empty ; continuations = 0L }

// FilterByLabel client "stocks.stockr.io/v1alpha1/stock/" 1000 0L [ ("locations.stockr.io/ismobile", Eq "false")]


// get a specific resource by its name
// stockApi.Get "10-01-001"
