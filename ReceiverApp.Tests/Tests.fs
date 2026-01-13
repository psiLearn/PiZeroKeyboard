module ReceiverApp.Tests

open System
open ReceiverApp
open ReceiverApp.HidMapping
open ReceiverApp.TextProcessor
open Xunit

let defaultLayout = KeyboardLayout.En

[<Fact>]
let ``toHid maps lowercase letters`` () =
    let result = HidMapping.toHid defaultLayout 'c'
    Assert.True(result.IsSome, "Expected mapping for 'c'.")
    let hid = result.Value
    Assert.Equal(0uy, hid.Modifier)
    Assert.Equal(0x06uy, hid.Key)

[<Fact>]
let ``toHid applies shift modifier for uppercase`` () =
    let result = HidMapping.toHid defaultLayout 'A'
    Assert.True(result.IsSome, "Expected mapping for 'A'.")
    let hid = result.Value
    Assert.Equal(0x02uy, hid.Modifier &&& 0x02uy)
    Assert.Equal(0x04uy, hid.Key)

[<Fact>]
let ``toHid normalizes carriage return`` () =
    let enter = HidMapping.toHid defaultLayout '\n'
    let carriage = HidMapping.toHid defaultLayout '\r'
    Assert.Equal(enter, carriage)

[<Fact>]
let ``toHid returns none for unsupported characters`` () =
    let result = HidMapping.toHid defaultLayout '\u0001'
    Assert.False(result.IsSome)

[<Fact>]
let ``processText forwards supported characters`` () =
    let sent = ResizeArray<HidKey>()
    let unsupported = ResizeArray<char>()

    TextProcessor.processText sent.Add unsupported.Add defaultLayout "Az\u0001"

    Assert.Equal<int>(2, sent.Count)
    Assert.Equal(0x04uy, sent.[0].Key)
    Assert.Equal(0x1Duy, sent.[1].Key)
    Assert.Single(unsupported) |> ignore
    Assert.Equal('\u0001', unsupported.[0])

[<Fact>]
let ``processText handles special tokens`` () =
    let sent = ResizeArray<HidKey>()
    let unsupported = ResizeArray<char>()

    TextProcessor.processText sent.Add unsupported.Add defaultLayout "A{BACKSPACE}B"

    Assert.Equal<int>(3, sent.Count)
    Assert.Equal(0x04uy, sent.[0].Key)
    Assert.Equal(0x2Auy, sent.[1].Key)
    Assert.Equal(0x05uy, sent.[2].Key)
    Assert.Equal(0x02uy, sent.[0].Modifier &&& 0x02uy)
    Assert.Equal(0x02uy, sent.[2].Modifier &&& 0x02uy)
    Assert.Empty(unsupported)

[<Fact>]
let ``processText escapes braces`` () =
    let sent = ResizeArray<HidKey>()
    let unsupported = ResizeArray<char>()

    TextProcessor.processText sent.Add unsupported.Add defaultLayout "{{}}"

    Assert.Equal<int>(2, sent.Count)
    Assert.Equal(0x2Fuy, sent.[0].Key)
    Assert.Equal(0x30uy, sent.[1].Key)
    Assert.Empty(unsupported)

[<Fact>]
let ``processText rejects null text`` () =
    let send _ = ()
    let log _ = ()
    let act () = TextProcessor.processText send log defaultLayout null
    Assert.Throws<ArgumentException>(Action act) |> ignore

[<Fact>]
let ``processText handles chord tokens`` () =
    let sent = ResizeArray<HidKey>()
    let unsupported = ResizeArray<char>()

    TextProcessor.processText sent.Add unsupported.Add defaultLayout "{CTRL+C}"

    Assert.Equal<int>(1, sent.Count)
    Assert.Equal(0x06uy, sent.[0].Key)
    Assert.Equal(0x01uy, sent.[0].Modifier &&& 0x01uy)
    Assert.Empty(unsupported)

[<Fact>]
let ``processText handles modifier-only tokens`` () =
    let sent = ResizeArray<HidKey>()
    let unsupported = ResizeArray<char>()

    TextProcessor.processText sent.Add unsupported.Add defaultLayout "{CTRL}"

    Assert.Single(sent) |> ignore
    Assert.Equal(0x01uy, sent.[0].Modifier &&& 0x01uy)
    Assert.Equal(0uy, sent.[0].Key)
    Assert.Empty(unsupported)

[<Fact>]
let ``processText keeps unknown chord token literal`` () =
    let sent = ResizeArray<HidKey>()
    let unsupported = ResizeArray<char>()
    let expected = "{CTRL+UNKNOWN}"

    TextProcessor.processText sent.Add unsupported.Add defaultLayout expected

    Assert.Equal(expected.Length, sent.Count)
    Assert.Equal(0x2Fuy, sent.[0].Key)
    Assert.Equal(0x02uy, sent.[0].Modifier &&& 0x02uy)
    Assert.Equal(0x30uy, sent.[sent.Count - 1].Key)
    Assert.Equal(0x02uy, sent.[sent.Count - 1].Modifier &&& 0x02uy)
    Assert.Empty(unsupported)

[<Fact>]
let ``processText switches layout via token`` () =
    let sent = ResizeArray<HidKey>()
    let unsupported = ResizeArray<char>()

    TextProcessor.processText sent.Add unsupported.Add defaultLayout "{LAYOUT=de}yz"

    Assert.Equal<int>(2, sent.Count)
    Assert.Equal(0x1Duy, sent.[0].Key)
    Assert.Equal(0x1Cuy, sent.[1].Key)
    Assert.Empty(unsupported)

[<Fact>]
let ``toHid uses german layout swaps y and z`` () =
    let yKey = HidMapping.toHid KeyboardLayout.De 'y'
    let zKey = HidMapping.toHid KeyboardLayout.De 'z'

    Assert.Equal(0x1Duy, yKey.Value.Key)
    Assert.Equal(0x1Cuy, zKey.Value.Key)

[<Fact>]
let ``toHid german layout maps umlauts`` () =
    let auml = HidMapping.toHid KeyboardLayout.De '\u00e4'
    let ouml = HidMapping.toHid KeyboardLayout.De '\u00f6'
    let uuml = HidMapping.toHid KeyboardLayout.De '\u00fc'
    let sz = HidMapping.toHid KeyboardLayout.De '\u00df'

    Assert.Equal(0x34uy, auml.Value.Key)
    Assert.Equal(0x33uy, ouml.Value.Key)
    Assert.Equal(0x2Fuy, uuml.Value.Key)
    Assert.Equal(0x2Duy, sz.Value.Key)

[<Fact>]
let ``toHid german layout maps euro`` () =
    let euro = HidMapping.toHid KeyboardLayout.De '\u20ac'

    Assert.Equal(0x08uy, euro.Value.Key)
    Assert.Equal(0x40uy, euro.Value.Modifier &&& 0x40uy)

[<Fact>]
let ``tryParseLayout accepts variants`` () =
    Assert.Equal(Some KeyboardLayout.De, HidMapping.tryParseLayout "de-de")
    Assert.Equal(Some KeyboardLayout.De, HidMapping.tryParseLayout "german")
    Assert.Equal(Some KeyboardLayout.En, HidMapping.tryParseLayout "us")
    Assert.Equal(Some KeyboardLayout.En, HidMapping.tryParseLayout "en-us")
    Assert.Equal(None, HidMapping.tryParseLayout "fr")
