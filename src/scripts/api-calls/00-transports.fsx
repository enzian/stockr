#r "nuget: FsHttp"
#r "nuget: Manifesto.Client.Fsharp"
#load "../../Stockr.Controller/Transport.fs"


open FsHttp
open transportation
open api


// List all transport orders
http {
    GET $"http://localhost:5000/apis/{transportation.api.Group}/{transportation.api.Version}/{transportation.api.Kind}/"
}
|> Request.send
|> Response.deserializeJson<Page<TransportFullManifest>>


// drop all transports
http {
    DELETE $"http://localhost:5000/apis/{transportation.api.Group}/{transportation.api.Version}/{transportation.api.Kind}/"
}
|> Request.send
|> Response.assert2xx