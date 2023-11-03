#r "nuget: FsHttp, 11.0.0"

#load "../Stockr/Api.fs"
#load "../Stockr/Stocks.fs"

open System.Net.Http
open System
open stock


let client = new HttpClient()
client.BaseAddress <- new Uri("https://localhost:7243/apis/")

let stockStatusApi =
    api.ManifestsFor<StockSpecManifest> client "logistics.stockr.io/v1alpha1/stock/status"

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
        { Location = ""
          Material = "p146723-11343"
          Amount = { qty = 12; unit = "pcs" } } }

stockStatusApi.Put stock
