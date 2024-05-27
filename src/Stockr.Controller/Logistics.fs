module logistics
open api
open stock

let MoveStockFromTo (stocksApi : ManifestApi<StockSpecManifest>) stock targetLocation =
    let moved_stock = { stock with spec.location = targetLocation }
    stocksApi.Put moved_stock
