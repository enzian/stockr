#r "nuget: dotnet-etcd"
#load "src/Stockr/Stocks.fs"
#load "src/Stockr/Locations.fs"
#load "src/Stockr/Persistence.fs"

open stock
open locations
open persistence
open dotnet_etcd

let etcdClient = new EtcdClient("https://localhost:2379")

let stockRepo = StockRepo etcdClient

let newStock = {
    Id = "lkasdjwj"
    Location = "1.02.011.0.L"
    Material = "A" |> Material
    Amount = (10 |> Quantity, "pcs" |> Unit)
    Labels = Map [||]
    Annotations = Map [||]
}
stockRepo.Create newStock

stockRepo.FindById "lkasdjwj"
stockRepo.FindByLocation "1.02.011.0.L"

stockRepo.Delete newStock.Id

let locationRepo = LocationRepo etcdClient

let location1 = {
    Id = "13.00.01"
    Labels = Map [
        ("location.stockr.io/v1alpha1/type", "forklift")
        ]
    Annotations = Map [("location.stockr.io/zonetype", "forklift")]
}
locationRepo.Create location1

// locationRepo.Delete "lkasdjwj"

locationRepo.FindById "13.00.01"
locationRepo.FindByLabel ("location.stockr.io/v1alpha1/type", Eq "forklift")

#load "src/Stockr/Logistics.fs"
open logistics

// MoveStock locationRepo stockRepo "lkasdjwj" "12.00.01"
let moveQty = MoveQuantity locationRepo stockRepo

moveQty "x9e3drpvgn" "10.00.01" "A" (3 |> Quantity, "pcs" |> Unit)
