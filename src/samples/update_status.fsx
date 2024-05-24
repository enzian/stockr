#r "nuget: Manifesto.Client.Fsharp"

#load "../Stockr.Controller/Stock.fs"

open System.Net.Http
open System
open stock
open System.Net

HttpClient.DefaultProxy <- new WebProxy()

let client= new HttpClient()
client.BaseAddress <- new Uri("http://localhost:5000/apis/")

let stockStatusApi =
    api.ManifestsFor<StockSpecManifest> client "stocks.stockr.io/v1alpha1/stock/"

let stock =
    { metadata =
        { name = "test1"
          ``namespace`` = None
          labels =
            Some(
                Map
                    [ ("locations.stockr.io/footprint", "tub")
                      ("locations.stockr.io/ismobile", "false") ]
            )
          annotations = None
          revision = None }
      spec =
        { location = ""
          material = "p146723-11343"
          quantity = "12pcs" } }

stockStatusApi.Put stock
