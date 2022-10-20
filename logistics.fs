module logistics
open stock
open locations
open persistence

type StockMovementError =
    | StockNotFound
    | TargetLocationNotFound
    | FailedToMoveStock

let MoveStock (locationRepo: LocationRepository) (stockRepo: StockRepository) stockId targetLocationId =
    match stockRepo.FindById stockId with
    | Error e -> Error(StockNotFound)
    | Ok (None) -> Error StockNotFound
    | Ok (Some stock) ->
        match locationRepo.FindById targetLocationId with
        | Error _ -> Error(TargetLocationNotFound)
        | Ok (None) -> Error TargetLocationNotFound
        | Ok (Some location) ->
            let movedStock = { stock with Location = location.Id }
            match stockRepo.Update movedStock with
            | Error _ -> Error(FailedToMoveStock)
            | Ok _ -> Ok()

let MoveQuantity (locationRepo: LocationRepository) (stockRepo: StockRepository) stockId targetLocationId (quantity : Amount) =
    match stockRepo.FindById stockId with
    | Error e -> Error(StockNotFound)
    | Ok (None) -> Error StockNotFound
    | Ok (Some stock) ->
        match locationRepo.FindById targetLocationId with
        | Error _ -> Error(TargetLocationNotFound)
        | Ok (None) -> Error TargetLocationNotFound
        | Ok (Some location) ->
            let movedStock = { stock with Location = location.Id }
            match stockRepo.Update movedStock with
            | Error _ -> Error(FailedToMoveStock)
            | Ok _ -> Ok()