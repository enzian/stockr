module logistics

open stock
open locations
open System
open api


type StockMovementError =
    | StockNotFound
    | TargetLocationNotFound
    | FailedToMoveStock
    | InsufficientQuantity
    | DisparateUnits
    | FailedToDropEmptySpace

// type LocationRepository = Repository<Location>
// type StockRepository = Repository<Stock>

// let MoveStock (locationRepo: LocationRepository) (stockRepo: StockRepository) stockId targetLocationId =
//     match stockRepo.FindById stockId with
//     | Error e -> Error(StockNotFound)
//     | Ok (None) -> Error StockNotFound
//     | Ok (Some { name = n ; metadata = m ; spec = Some s}) ->
//         match locationRepo.FindById targetLocationId with
//         | Error _ -> Error(TargetLocationNotFound)
//         | Ok (None) -> Error TargetLocationNotFound
//         | Ok (Some { spec = Some location}) ->
//             let movedStock = {
//                 name = n
//                 metadata = m
//                 spec = Some { s with Location = location.Id } }

//             match stockRepo.Update movedStock with
//             | Error _ -> Error(FailedToMoveStock)
//             | Ok _ -> Ok()
//         | _ -> Error FailedToMoveStock

// let FindStockByLocation (stockRepo : Repository<Stock>) location =
//     try
//         let allStocks = stockRepo.List
//         match allStocks with
//         | Ok stocks -> 
//             Ok (
//                 stocks
//                 |> Seq.filter (fun x -> 
//                     match x.spec with 
//                     | Some s -> s.Location = location
//                     | None -> false))
//         | Error e -> Error e
//     with ex ->
//         Error ex
// let MoveQuantity
//     (locationRepo: LocationRepository)
//     (stockRepo: StockRepository)
//     (stockId: string)
//     (targetLocationId: string)
//     (material: string)
//     (qty: double)
//     (unit: string)
//     =

//     match stockRepo.FindById stockId with
//     | Error e -> Error StockNotFound
//     | Ok (None) -> Error StockNotFound
//     | Ok (Some { name = stockName; metadata = stockMetadata; spec = Some stock}) ->
//         match ((qty, unit), (stock.Amount.qty, stock.Amount.unit)) with
//         | ((qty, _), (sqty, _)) when sqty < qty -> Error InsufficientQuantity
//         | ((_, unit), (_, sunit)) when unit <> sunit -> Error DisparateUnits
//         | ((qty, unit), (sqty, _)) ->
//             match locationRepo.FindById targetLocationId with
//             | Error _ -> Error TargetLocationNotFound
//             | Ok (None) -> Error TargetLocationNotFound
//             | Ok (Some { spec = Some targetLocationSpec}) ->
//                 match FindStockByLocation stockRepo targetLocationSpec.Id with
//                 | Error _ -> Error TargetLocationNotFound
//                 | Ok stocks ->
//                     let x =
//                         stocks
//                         |> Seq.filter (fun x -> 
//                             match x.spec with
//                             | Some s -> s.Material = stock.Material
//                             | None -> false)
//                         |> Seq.tryLast
//                     match x with
//                     | None ->
//                         let id = Guid.NewGuid().ToString()
//                         let newStock = {
//                                 name = id
//                                 metadata = {
//                                     labels = Map<string, string> []
//                                     annotations = Map<string, string> [] }
//                                 spec = Some {
//                                     Location = targetLocationId
//                                     Material = stock.Material
//                                     Amount = {
//                                         qty = qty
//                                         unit = unit}}}

//                         stockRepo.Create newStock |> ignore
//                     | Some {name = n; metadata = m; spec = Some s} ->
//                         let { qty = tqty } = s.Amount
//                         let { qty = qty } = amount
//                         let targetStock = {
//                             name = n
//                             metadata = m
//                             spec = Some { s with Amount = { qty = (tqty + qty) ; unit = unit } }}
//                         stockRepo.Update targetStock |> ignore

//                     let remainingQty = sqty - qty
//                     match remainingQty with
//                     | x when x > 0 ->
//                         let deductedSourceStock = {
//                             name = stockName 
//                             metadata = stockMetadata
//                             spec = Some { stock with Amount = { qty = x ; unit = unit } }}

//                         match stockRepo.Update deductedSourceStock with
//                         | Error _ -> Error FailedToMoveStock
//                         | Ok _ -> Ok()
//                     | x when x < 0 ->
//                         match stockRepo.Delete stockName with
//                         | Error _ -> Error FailedToDropEmptySpace
//                         | Ok _ -> Ok()
//                     | _ -> Error FailedToMoveStock


type StockManifest = 
    { spec: ApiStock
      metadata: Metadata }
    interface Manifest with 
        member this.metadata = this.metadata 

let MoveQuantity (apiClient: api.ManifestApi<StockManifest>) fromStock toStock quantity =
    let srcStockRes = apiClient.Get fromStock
    let targetStockRes = apiClient.Get toStock

    ()

