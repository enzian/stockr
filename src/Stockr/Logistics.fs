module logistics

open stock
open locations
open persistence
open System

type StockMovementError =
    | StockNotFound
    | TargetLocationNotFound
    | FailedToMoveStock
    | InsufficientQuantity
    | DisparateUnits
    | FailedToDropEmptySpace

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

let MoveQuantity
    (locationRepo: LocationRepository)
    (stockRepo: StockRepository)
    (stockId: string)
    (targetLocationId: string)
    (material: string)
    (amount: Amount)
    =

    match stockRepo.FindById stockId with
    | Error e -> Error StockNotFound
    | Ok (None) -> Error StockNotFound
    | Ok (Some stock) ->
        match (amount, stock.Amount) with
        | ((qty, _), (sqty, _)) when sqty < qty -> Error InsufficientQuantity
        | ((_, unit), (_, sunit)) when unit <> sunit -> Error DisparateUnits
        | ((qty, unit), (sqty, _)) ->
            match locationRepo.FindById targetLocationId with
            | Error _ -> Error TargetLocationNotFound
            | Ok (None) -> Error TargetLocationNotFound
            | Ok (Some targetLocation) ->
                match stockRepo.FindByLocation targetLocation.Id with
                | Error _ -> Error TargetLocationNotFound
                | Ok stocks ->
                    match stocks |> Seq.filter (fun x -> x.Material = stock.Material) |> Seq.tryLast with
                    | None ->
                        let newStock =
                            { Id = Guid.NewGuid().ToString()
                              Location = targetLocationId
                              Material = stock.Material
                              Amount = amount
                              Labels = Map<string, string> []
                              Annotations = Map<string, string> [] }

                        stockRepo.Create newStock |> ignore
                    | Some tail ->
                        let (tqty, _) = tail.Amount
                        let (qty, _) = amount
                        let targetStock = { tail with Amount = ((tqty + qty), unit) }
                        stockRepo.Update targetStock |> ignore

                    let remainingQty = sqty - qty
                    match remainingQty with
                    | Quantity (x) when x > 0 ->
                        let deductedSourceStock = { stock with Amount = (x |> Quantity, unit) }

                        match stockRepo.Update deductedSourceStock with
                        | Error _ -> Error FailedToMoveStock
                        | Ok _ -> Ok()
                    | Quantity (x) when x < 0 ->
                        match stockRepo.Delete stock.Id with
                        | Error _ -> Error FailedToDropEmptySpace
                        | Ok _ -> Ok()
                    | _ -> Error FailedToMoveStock
