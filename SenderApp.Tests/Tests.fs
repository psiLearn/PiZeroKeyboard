module SenderApp.Tests

open SenderApp.CapsLockModel
open SenderApp.UsbStatusModel
open Xunit

[<Fact>]
let ``statusFromState maps configured`` () =
    let result = statusFromState "configured"
    Assert.Equal("Raspberry Pi USB: connected (configured)", result.Text)
    Assert.Equal("connected", result.CssClass)

[<Fact>]
let ``statusFromState maps not attached`` () =
    let result = statusFromState "not attached"
    Assert.Equal("Raspberry Pi USB: not attached", result.Text)
    Assert.Equal("disconnected", result.CssClass)

[<Fact>]
let ``statusFromState maps pending states`` () =
    let attached = statusFromState "attached"
    let powered = statusFromState "powered"
    let def = statusFromState "default"

    Assert.Equal("Raspberry Pi USB: attached (not configured yet)", attached.Text)
    Assert.Equal("pending", attached.CssClass)
    Assert.Equal("Raspberry Pi USB: powered (not configured yet)", powered.Text)
    Assert.Equal("pending", powered.CssClass)
    Assert.Equal("Raspberry Pi USB: default (not configured yet)", def.Text)
    Assert.Equal("pending", def.CssClass)

[<Fact>]
let ``statusFromState maps empty and null`` () =
    let empty = statusFromState ""
    let whitespace = statusFromState "  "
    let nullState = statusFromState null

    Assert.Equal("Raspberry Pi USB: unknown (empty state)", empty.Text)
    Assert.Equal("unknown", empty.CssClass)
    Assert.Equal("Raspberry Pi USB: unknown (empty state)", whitespace.Text)
    Assert.Equal("unknown", whitespace.CssClass)
    Assert.Equal("Raspberry Pi USB: unknown (empty state)", nullState.Text)
    Assert.Equal("unknown", nullState.CssClass)

[<Fact>]
let ``statusFromState maps unknown`` () =
    let result = statusFromState "mystery"
    Assert.Equal("Raspberry Pi USB: mystery", result.Text)
    Assert.Equal("unknown", result.CssClass)

[<Fact>]
let ``statusMissingStateFile returns unknown`` () =
    let result = statusMissingStateFile ()
    Assert.Equal("Raspberry Pi USB: unknown (state file not found)", result.Text)
    Assert.Equal("unknown", result.CssClass)

[<Fact>]
let ``statusReadError returns unknown`` () =
    let result = statusReadError "boom"
    Assert.Equal("Raspberry Pi USB: unknown (boom)", result.Text)
    Assert.Equal("unknown", result.CssClass)

[<Fact>]
let ``caps lock maps on`` () =
    let result = statusFromValue "on"
    Assert.Equal("Caps Lock: on", result.Text)
    Assert.Equal("on", result.CssClass)

[<Fact>]
let ``caps lock maps off`` () =
    let result = statusFromValue "off"
    Assert.Equal("Caps Lock: off", result.Text)
    Assert.Equal("off", result.CssClass)

[<Fact>]
let ``caps lock maps unknown`` () =
    let empty = statusFromValue ""
    let other = statusFromValue "mystery"
    Assert.Equal("Caps Lock: unknown", empty.Text)
    Assert.Equal("unknown", empty.CssClass)
    Assert.Equal("Caps Lock: mystery", other.Text)
    Assert.Equal("unknown", other.CssClass)
