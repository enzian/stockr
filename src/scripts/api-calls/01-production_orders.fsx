#r "nuget: FsHttp"
#r "nuget: Manifesto.Client.Fsharp"
#load "../../Stockr.Controller/Measurement.fs"
#load "../../Stockr.Controller/ProductionOrder.fs"

open FsHttp
open api
open production


// List all production orders
http { GET $"http://localhost:5000/apis/{production.api.Group}/{production.api.Version}/{production.api.Kind}/" }
|> Request.send
|> Response.assert2xx
|> Response.deserializeJson<Page<api.ProductionOrderFullManifest>>


http {
    PUT $"http://localhost:5000/apis/{production.api.Group}/{production.api.Version}/{production.api.Kind}/&limit=100"
    body

    jsonSerialize
        ({
            metadata =
                { name = "prod01"
                  labels = None
                  annotations = None
                  ``namespace`` = None
                  revision = None }
            spec =
                { material = "Cable-Assembly"
                  amount = "5pcs"
                  from = "20-01-01"
                  target = "20-01-02"
                  bom = [
                    { material = "Header"; quantity = "2pcs" }
                    { material = "Cable"; quantity = "1m" } ] } } : api.ProductionOrderSpecManifest)
}
|> Request.send
|> Response.assert2xx

http {
    PUT $"http://localhost:5000/apis/{production.api.Group}/{production.api.Version}/{production.api.Kind}/status"
    body

    jsonSerialize
        ({
            metadata =
                { name = "prod01"
                  labels = None
                  annotations = None
                  ``namespace`` = None
                  revision = None }
            status = Some {
                state = "started"
                amount = "5pcs"
                reason = None } } : api.ProductionOrderStateManifest)
}
|> Request.send
|> Response.assert2xx

// Delete all production orders
http {
    DELETE $"http://localhost:5000/apis/{production.api.Group}/{production.api.Version}/{production.api.Kind}/"
}
|> Request.send
|> Response.assert2xx