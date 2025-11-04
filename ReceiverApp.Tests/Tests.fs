module ReceiverApp.Tests

open System
open ReceiverApp
open ReceiverApp.HidMapping
open ReceiverApp.TextProcessor
open Xunit

[<Fact>]
let ``toHid maps lowercase letters`` () =
    let result = HidMapping.toHid 'c'
    Assert.True(result.IsSome, "Expected mapping for 'c'.")
    let hid = result.Value
    Assert.Equal(0uy, hid.Modifier)
    Assert.Equal(0x06uy, hid.Key)

[<Fact>]
let ``toHid applies shift modifier for uppercase`` () =
    let result = HidMapping.toHid 'A'
    Assert.True(result.IsSome, "Expected mapping for 'A'.")
    let hid = result.Value
    Assert.Equal(0x02uy, hid.Modifier &&& 0x02uy)
    Assert.Equal(0x04uy, hid.Key)

[<Fact>]
let ``toHid normalizes carriage return`` () =
    let enter = HidMapping.toHid '\n'
    let carriage = HidMapping.toHid '\r'
    Assert.Equal(enter, carriage)

[<Fact>]
let ``toHid returns none for unsupported characters`` () =
    let result = HidMapping.toHid '€'
    Assert.False(result.IsSome)

[<Fact>]
let ``processText forwards supported characters`` () =
    let sent = ResizeArray<HidKey>()
    let unsupported = ResizeArray<char>()

    TextProcessor.processText sent.Add unsupported.Add "Az€"

    Assert.Equal<int>(2, sent.Count)
    Assert.Equal(0x04uy, sent.[0].Key)
    Assert.Equal(0x1Duy, sent.[1].Key)
    Assert.Single(unsupported)
    Assert.Equal('€', unsupported.[0])

[<Fact>]
let ``processText rejects null text`` () =
    let send _ = ()
    let log _ = ()
    let act () = TextProcessor.processText send log null
    Assert.Throws<ArgumentException>(Action act) |> ignore
