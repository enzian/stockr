#r "nuget: FsHttp, 11.0.0"

#load "../Stockr/Api.fs"

open System.Net.Http
open System

type StockSpec = { material: string; qty : string }
type StockStatus = { B: string }

let client= new HttpClient()
client.BaseAddress <- new Uri("https://localhost:7243/apis/")

let stockStatusApi = 
    api.ManifestsFor<StockSpec, StockStatus> 
        client
        "logistics.stockr.io/v1alpha1/stock/status"

let stock: api.Manifest<StockSpec, StockStatus> = { 
    kind = "stock"
    apigroup = null
    apiversion = null
    metadata = { 
        name = "test1"
        ``namespace`` = None
        labels = 
            Some (Map [("locations.stockr.io/footprint", "tub");
             ("locations.stockr.io/ismobile", "false")])
        annotations = None
        revision = None}
    spec = Some {
        material = "p146723-11343"
        qty = "12pcs" }
    status = Some { B = DateTime.Now.ToFileTimeUtc().ToString() } }

stockStatusApi.Put stock
