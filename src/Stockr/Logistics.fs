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

type LocationRepository = Repository<Location>
type StockRepository = Repository<Stock>

let MoveStock (locationRepo: LocationRepository) (stockRepo: StockRepository) stockId targetLocationId =
    match stockRepo.FindById stockId with
    | Error e -> Error(StockNotFound)
    | Ok (None) -> Error StockNotFound
    | Ok (Some stock) ->
        match locationRepo.FindById targetLocationId with
        | Error _ -> Error(TargetLocationNotFound)
        | Ok (None) -> Error TargetLocationNotFound
        | Ok (Some location) ->
            let movedStock = { stock with spec = {stock.spec with Location = location.name} }

            match stockRepo.Update movedStock with
            | Error _ -> Error(FailedToMoveStock)
            | Ok _ -> Ok()

let FindStockByLocation (stockRepo : Repository<Stock>) location =
    try
        let allStocks = stockRepo.List
        match allStocks with
        | Ok stocks -> Ok (stocks |> Seq.filter (fun x -> x.spec.Location = location))
        | Error e -> Error e
    with ex ->
        Error ex.Message


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
        match ((amount.qty, amount.unit), (stock.spec.Amount.qty, stock.spec.Amount.unit)) with
        | ((qty, _), (sqty, _)) when sqty < qty -> Error InsufficientQuantity
        | ((_, unit), (_, sunit)) when unit <> sunit -> Error DisparateUnits
        | ((qty, unit), (sqty, _)) ->
            match locationRepo.FindById targetLocationId with
            | Error _ -> Error TargetLocationNotFound
            | Ok (None) -> Error TargetLocationNotFound
            | Ok (Some targetLocation) ->
                match FindStockByLocation stockRepo targetLocation.spec.Id with
                | Error _ -> Error TargetLocationNotFound
                | Ok stocks ->
                    match stocks |> Seq.filter (fun x -> x.spec.Material = stock.spec.Material) |> Seq.tryLast with
                    | None ->
                        let id = Guid.NewGuid().ToString()
                        let newStock = {
                                name = id
                                metadata = {
                                    labels = Map<string, string> []
                                    annotations = Map<string, string> [] }
                                spec = {
                                    Location = targetLocationId
                                    Material = stock.spec.Material
                                    Amount = amount}}

                        stockRepo.Create newStock |> ignore
                    | Some tail ->
                        let { qty = tqty } = tail.spec.Amount
                        let { qty = qty } = amount
                        let targetStock = { tail with spec = { tail.spec with Amount = { qty = (tqty + qty) ; unit = unit } }}
                        stockRepo.Update targetStock |> ignore

                    let remainingQty = sqty - qty
                    match remainingQty with
                    | Quantity (x) when x > 0 ->
                        let deductedSourceStock = { stock with spec = { stock.spec with Amount = { qty = x |> Quantity; unit = unit } }}

                        match stockRepo.Update deductedSourceStock with
                        | Error _ -> Error FailedToMoveStock
                        | Ok _ -> Ok()
                    | Quantity (x) when x < 0 ->
                        match stockRepo.Delete stock.name with
                        | Error _ -> Error FailedToDropEmptySpace
                        | Ok _ -> Ok()
                    | _ -> Error FailedToMoveStock
