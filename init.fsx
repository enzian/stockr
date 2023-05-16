#r "nuget: dotnet-etcd"
#load "src/Stockr/Stocks.fs"
#load "src/Stockr/Locations.fs"
#load "src/Stockr/Persistence.fs"

open stock
open locations
open persistence
open dotnet_etcd

let etcdClient = new EtcdClient("https://localhost:2379")

let stockRepo = newRepository<stock.Stock> etcdClient "/stocks/"

let newStock = {
    name = "lkasdjwj"
    metadata = {
        labels = Map [||]
        annotations = Map [||]
    }
    spec = {
        Location = "13.00.01"
        Material = "A" |> Material
        Amount = { qty = (10 |> Quantity); unit = "pcs" |> Unit }
    }
}
stockRepo.Create newStock

stockRepo.FindById "lkasdjwj"
logistics.FindStockByLocation stockRepo "13.00.02"


stockRepo.Delete newStock.name

let locationRepo = newRepository<Location> etcdClient "/locations/"

let location1 = {
    name = "13.00.01"
    metadata = {
        labels = Map [("location.stockr.io/v1alpha1/type", "forklift")]
        annotations = Map [("location.stockr.io/zonetype", "forklift")]
    }
    spec = { Id = "13.00.01" }}
locationRepo.Create location1
let location2 = {
    name = "13.00.02"
    metadata = { labels = Map [] ; annotations = Map [] }
    spec = { Id = "13.00.02" }}
locationRepo.Create location2

// locationRepo.Delete "lkasdjwj"

locationRepo.FindById "13.00.02"
locationRepo.FindByLabel ("location.stockr.io/v1alpha1/type", Eq "forklift")

#load "src/Stockr/Logistics.fs"
open logistics

// MoveStock locationRepo stockRepo "lkasdjwj" "12.00.01"
let moveQty = MoveQuantity locationRepo stockRepo

moveQty "x9e3drpvgn" "10.00.01" "A" { qty = 3 |> Quantity; unit = "pcs" |> Unit }
