#r "nuget: MongoDB.Driver"
#load "stock.fs"

open stock

#load "locations.fs"
open locations

#load "persistence.fs"

open persistence

let db = Open "mongodb://localhost:27017" "stockr"
let stockCol = db.GetCollection<StockModel>("stocks")
let stockRepo = StockRepo stockCol

let stock = {
    Id = "rrlaihbrdq"
    Location = "10.00.01"
    Material = "A" |> Material
    Amount = (10 |> Quantity, "pcs" |> Unit)
}

stockRepo.Create stock


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


let locationCol = db.GetCollection<LocationModel>("locations")
let locationRepo = LocationRepo locationCol

// let location1 = {
//     Id = "12.00.01"
//     Labels = Map [("zone", "12"); ("rack", "0"); ("space", "1")]
//     Annotations = Map [("zone", "pallet storage")]
// }
// locationRepo.Create location1

// locationRepo.Delete "lkasdjwj"

// locationRepo.FindById "10.00.01"

#load "logistics.fs"
open logistics

// MoveStock locationRepo stockRepo "lkasdjwj" "12.00.01"
MoveQuantity locationRepo stockRepo "x9e3drpvgn" "10.00.01" "A" (3 |> Quantity, "pcs" |> Unit)
