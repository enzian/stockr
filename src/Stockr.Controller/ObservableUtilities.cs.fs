module utilities

open api
open System.Threading
open FSharp.Control.Reactive
open utilities
open FSharp.Control.Reactive.Observable

let appendToDict<'T when 'T :> Manifest> (d: Map<string, 'T>) (e: Event<'T>) =
    match e with
    | Update m -> d.Add(m.metadata.name, m)
    | Create m -> d.Add(m.metadata.name, m)
    | Delete m -> d.Remove(m.metadata.name)

let watchResourceOfType<'T when 'T :> Manifest> (api: ManifestApi<'T>) (token: CancellationToken) =
    let initialResources = api.List
    let startRevision = initialResources |> mostRecentRevision
    let initialResourcesObs =
        Subject.behavior (initialResources |> Seq.map (fun x -> (x.metadata.name, x)) |> Map.ofSeq)
    
    let watchObs = api.WatchFromRevision startRevision token |> Async.RunSynchronously |> publish
    
    let aggregate = 
        merge
            (watchObs
             |> scanInit (initialResources |> Seq.map (fun x -> (x.metadata.name, x)) |> Map.ofSeq) mapEventToDict)
            initialResourcesObs
    watchObs |> connect |> ignore

    (aggregate, watchObs)

