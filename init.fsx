#r "nuget: MongoDB.Driver";;
#load "stock.fs"
open stock


#load "persistence.fs";;
open persistence

let db = Open "mongodb://localhost:27017" "stockr"
let stockCol = db.GetCollection<StockModel>("stocks")
let stockRepo = StockRepo stockCol

let stock = {
    Id = "lkasdjwj"
    Location = "test"
    Material = "A" |> Material
    Amount = (10 |> Quantity, "pcs" |> Unit)
}

stockRepo.Create stock

