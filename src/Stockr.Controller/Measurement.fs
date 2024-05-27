module measurement
open System
open System.Text.RegularExpressions

type Quantity = (decimal * string)

let decRegex = new Regex(@"^([\d\.]*)\s*([\w\/_]*)$", RegexOptions.Compiled)

let amountFromString (str: string) = 
    let matches = decRegex.Match(str)
    if matches.Success then
        let dec = Decimal.Parse(matches.Groups.[1].Value)
        let unit = matches.Groups.[2].Value
        Some (dec, unit)
    else
        None

let toQuantity (str: string) = 
    match amountFromString str with
    | Some (d, u) -> Quantity (d, u)
    | None -> failwithf "Could not parse %s" str

let toString (d, u) = sprintf "%.2f%s" d u
let quantityToString (x : Quantity) = toString x

let convert fromUnit targetUnit qty =
    let factor = 
        match fromUnit, targetUnit with
        | "cm", "mm" -> 10m
        | "m", "mm" -> 1000m
        | "m", "cm" -> 100m
        | "pcs", "pcs" -> 1m
        | "cm", "cm" -> 1m
        | "cm", "m" -> 0.01m
        | _ -> failwithf "Could not convert %s to %s" fromUnit targetUnit
    qty * factor

let convertQuantity toUnit (d, fromUnit) =
    let convertedQty = convert fromUnit toUnit d
    Quantity (convertedQty, toUnit)


    