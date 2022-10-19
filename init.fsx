#r "nuget: MongoDB.Driver";;
#load "stock.fs"
open stock

#load "locations.fs"
open locations

#load "persistence.fs";;
open persistence

let db = Open "mongodb://localhost:27017" "stockr"
// let stockCol = db.GetCollection<StockModel>("stocks")
// let stockRepo = StockRepo stockCol

// let stock = {
//     Id = "lkasdjwj"
//     Location = "test"
//     Material = "A" |> Material
//     Amount = (10 |> Quantity, "pcs" |> Unit)
// }

// stockRepo.Create stock


// let newStock = {
//     Id = "lkasdjwj"
//     Location = "1.02.011.0.L"
//     Material = "A" |> Material
//     Amount = (10 |> Quantity, "pcs" |> Unit)
// }
// stockRepo.Update newStock

// stockRepo.FindById "lkasdjwj"

// stockRepo.Delete stock.Id


let locationCol = db.GetCollection<LocationModel>("locations")
let locationRepo = LocationRepo locationCol

let location = {
    Id = "10.00.01"
    Labels = Map [("a", "b")]
    Annotations = Map [("a", "b")]
}
locationRepo.Create location
let location1 = {
    Id = "12.1.2"
    Labels = Map [("a", "b")]
    Annotations = Map [("a", "b")]
}
locationRepo.Create location1