#r "nuget: MongoDB.Driver"
#r "nuget: FsHttp"
#load "src/Stockr/Stocks.fs"

open stock

#load "src/Stockr/Locations.fs"
open locations

#load "src/Stockr/Persistence.fs"
open persistence
open System.Net.Http
open System

let db = Open "mongodb://localhost:27017" "stockr"
// let stockCol = db.GetCollection<StockModel>("stocks")
let stockRepo = StockRepo "http://localhost:2379/v3/"

// let newStock = {
//     Id = "lkasdjwj"
//     Location = "1.02.011.0.L"
//     Material = "A" |> Material
//     Amount = (10 |> Quantity, "pcs" |> Unit)
// }
// stockRepo.Update newStock

// stockRepo.FindById "lkasdjwj"

// stockRepo.Delete stock.Id
// stockRepo.FindByLocation "lkasdjwj"
stockRepo.FindByLocation "10.00.01"


let locationCol = db.GetCollection<LocationModel>("locations")
let locationRepo = LocationRepo locationCol

let location1 = {
    Id = "13.00.01"
    Labels = Map [
        ("location.stockr.io/v1alpha1/type", "forklift")
        ]
    Annotations = Map [("location.stockr.io/zonetype", "forklift")]
}
locationRepo.Create location1

// locationRepo.Delete "lkasdjwj"

// locationRepo.FindById "10.00.01"
locationRepo.FindByLabel ("location.stockr.io/v1alpha1/type", Eq "forklift")

#load "src/Stockr/Logistics.fs"
open logistics

// MoveStock locationRepo stockRepo "lkasdjwj" "12.00.01"
let moveQty = MoveQuantity locationRepo stockRepo

moveQty "x9e3drpvgn" "10.00.01" "A" (3 |> Quantity, "pcs" |> Unit)