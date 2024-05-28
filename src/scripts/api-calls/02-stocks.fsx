#r "nuget: FsHttp"
#r "nuget: Manifesto.Client.Fsharp"
#load "../../Stockr.Controller/Stock.fs"


open FsHttp
open stock
open api


// List all transport orders
http {
    GET $"http://localhost:5000/apis/{stock.api.Group}/{stock.api.Version}/{stock.api.Kind}/?continuation=0&limit=100"
}
|> Request.send
|> Response.deserializeJson<Page<StockSpecManifest>>


http {
    PUT $"http://localhost:5000/apis/{stock.api.Group}/{stock.api.Version}/{stock.api.Kind}/"
    body

    jsonSerialize
        ({
            metadata =
                { name = "cables"
                  labels = None
                  annotations = None
                  ``namespace`` = None
                  revision = None }
            spec =
                { material = "Cable"
                  quantity = "10m"
                  location = "10-00-01" } } : StockSpecManifest)
}
|> Request.send
|> Response.assert2xx

http {
    DELETE $"http://localhost:5000/apis/{stock.api.Group}/{stock.api.Version}/{stock.api.Kind}/"
}
|> Request.send
|> Response.assert2xx