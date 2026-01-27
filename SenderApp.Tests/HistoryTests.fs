namespace SenderApp.Tests

open Xunit
open SenderApp.Client.HistoryCore

module HistoryTests =

    let private nowValue = 4242.0
    let private now () = nowValue

    let private mkItem text ts =
        { text = text; timestamp = Some ts }

    [<Fact>]
    let ``normalizeText trims and ignores empty`` () =
        Assert.Equal(None, normalizeText null)
        Assert.Equal(None, normalizeText "")
        Assert.Equal(None, normalizeText "   ")
        Assert.Equal(Some "hello", normalizeText "  hello  ")

    [<Fact>]
    let ``clampIndex respects range`` () =
        Assert.Equal(0, clampIndex -5 0)
        Assert.Equal(0, clampIndex -5 3)
        Assert.Equal(2, clampIndex 2 3)
        Assert.Equal(3, clampIndex 10 3)

    [<Fact>]
    let ``addEntry appends and updates index`` () =
        let items = [ mkItem "first" 1.0 ]
        let state = addEntry now items "second"

        Assert.Equal(2, state.items.Length)
        Assert.Equal("second", state.items |> List.last |> fun item -> item.text)
        Assert.Equal(nowValue, state.items |> List.last |> fun item -> item.timestamp |> Option.defaultValue -1.0)
        Assert.Equal(1, state.index)

    [<Fact>]
    let ``addEntry deduplicates and keeps last index`` () =
        let items = [ mkItem "alpha" 1.0; mkItem "beta" 2.0 ]
        let state = addEntry now items "beta"

        Assert.Equal(2, state.items.Length)
        Assert.Equal("beta", state.items |> List.last |> fun item -> item.text)
        Assert.Equal(1, state.index)

    [<Fact>]
    let ``addEntry prunes to maxHistoryItems`` () =
        let items =
            [ for i in 1 .. maxHistoryItems -> mkItem (sprintf "item-%d" i) (float i) ]
        let state = addEntry now items "overflow"

        Assert.Equal(maxHistoryItems, state.items.Length)
        Assert.Equal("overflow", state.items |> List.last |> fun item -> item.text)
        Assert.Equal(sprintf "item-%d" 2, state.items.Head.text)

    [<Fact>]
    let ``movePrev and moveNext clamp within range`` () =
        let items = [ mkItem "a" 1.0; mkItem "b" 2.0; mkItem "c" 3.0 ]

        Assert.Equal(0, movePrev 0 items)
        Assert.Equal(1, movePrev 2 items)
        Assert.Equal(2, moveNext 2 items)
        Assert.Equal(1, moveNext 0 items)

    [<Fact>]
    let ``movePrev and moveNext return zero for empty list`` () =
        let empty: HistoryItem list = []
        Assert.Equal(0, movePrev 0 empty)
        Assert.Equal(0, moveNext 0 empty)
