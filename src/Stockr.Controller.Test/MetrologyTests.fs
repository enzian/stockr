module metrology

open Xunit
open measurement
open FsUnit

[<Theory>]
[<InlineData("2pcs", 2.0, "pcs")>]
[<InlineData("2.1m", 2.1, "m")>]
[<InlineData("2.1m", 2.1, "m")>]
[<InlineData("2.m", 2.0, "m")>]
let ``Converting Measures from strings`` (input: string) (value: float) (unit: string) =
    let (Measure (dec, u)) = input |> toMeasure
    dec |> should equal value
    u |> should equal unit

[<Theory>]
[<InlineData(1.0, "m", 1000, "mm")>]
[<InlineData(1.0, "cm", 10, "mm")>]
[<InlineData(1.0, "m", 100, "cm")>]
let ``Convert Unit Of Length`` (rightVal : decimal) (rightU: string) (leftVal: decimal) (leftU: string) =
    let (Measure (v,u)) = (Measure (rightVal, rightU)) |> convertMeasure leftU
    v |> should equal leftVal
    u |> should equal leftU
