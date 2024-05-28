module utilities
open System
open api
open System.Threading
open FSharp.Control.Reactive
open utilities
open FSharp.Control.Reactive.Observable

let randomStr n = 
    let r = Random()
    let chars = Array.concat([[|'a' .. 'z'|];[|'A' .. 'Z'|];[|'0' .. '9'|]])
    let sz = Array.length chars in
    String(Array.init n (fun _ -> chars.[r.Next sz]))

let appendToDict<'T when 'T :> Manifest> (d: Map<string, 'T>) (e: Event<'T>) =
    match e with
    | Update m -> d.Add(m.metadata.name, m)
    | Create m -> d.Add(m.metadata.name, m)
    | Delete m -> d.Remove(m.metadata.name)

let watchResourceOfType<'T when 'T :> Manifest> (api: ManifestApi<'T>) (token: CancellationToken) =
    let initialResources = api.List token 0 1000
    let startRevision = initialResources.continuations
    let initialResourcesObs =
        Subject.behavior (initialResources.items |> Seq.map (fun x -> (x.metadata.name, x)) |> Map.ofSeq)
    
    let watchObs = api.WatchFromRevision ((int64)startRevision) token |> Async.RunSynchronously |> publish
    
    let aggregate = 
        merge
            (watchObs
             |> scanInit (initialResources.items |> Seq.map (fun x -> (x.metadata.name, x)) |> Map.ofSeq) mapEventToDict)
            initialResourcesObs
    watchObs |> connect |> ignore

    (aggregate, watchObs)