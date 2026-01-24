namespace SenderApp.Tests

open System.Collections.Generic
open System.IO
open Jint
open Jint.Native
open Xunit

type FakeStorage() =
    let store = Dictionary<string, string>()

    member _.getItem(key: string) =
        match store.TryGetValue key with
        | true, value -> value
        | _ -> null

    member _.setItem(key: string, value: string) =
        store.[key] <- value

    member _.SetRaw(key: string, value: string) =
        store.[key] <- value

module HistoryTests =

    let historyKey = "linuxkey-history"
    let historyIndexKey = "linuxkey-history-index"

    let loadHistoryEngine (storage: FakeStorage) =
        let engine = new Engine()
        let scriptPath =
            Path.Combine(__SOURCE_DIRECTORY__, "..", "SenderApp", "wwwroot", "history.js")
        let script = File.ReadAllText(scriptPath)
        engine.Execute(script) |> ignore
        engine.SetValue("localStorage", storage) |> ignore
        engine

    let invokeHistory (engine: Engine) name (args: obj array) =
        let history = engine.GetValue("LinuxKeyHistory").AsObject()
        let key = JsValue.FromObject(engine, name)
        let fn = history.Get(key)
        engine.Invoke(fn, history, args)

    let jsArrayToStrings (value: JsValue) =
        let array = value.AsArray()
        let length = int (array.Get("length").AsNumber())
        [ for i in 0 .. length - 1 -> array.Get(i).ToString() ]

    let jsState (value: JsValue) =
        let obj = value.AsObject()
        let items = jsArrayToStrings (obj.Get("items"))
        let index = int (obj.Get("index").AsNumber())
        items, index

    [<Fact>]
    let ``history read ignores missing or invalid data`` () =
        let storage = FakeStorage()
        let engine = loadHistoryEngine storage

        let empty =
            invokeHistory engine "readHistory" [| storage :> obj; historyKey :> obj |]
            |> jsArrayToStrings
        Assert.Empty(empty)

        storage.SetRaw(historyKey, "not-json")
        let invalid =
            invokeHistory engine "readHistory" [| storage :> obj; historyKey :> obj |]
            |> jsArrayToStrings
        Assert.Empty(invalid)

        storage.SetRaw(historyKey, "\"hello\"")
        let notArray =
            invokeHistory engine "readHistory" [| storage :> obj; historyKey :> obj |]
            |> jsArrayToStrings
        Assert.Empty(notArray)

    [<Fact>]
    let ``loadHistoryState clamps stored index`` () =
        let storage = FakeStorage()
        storage.SetRaw(historyKey, "[\"one\",\"two\"]")
        storage.SetRaw(historyIndexKey, "99")
        let engine = loadHistoryEngine storage

        let items, index =
            invokeHistory
                engine
                "loadHistoryState"
                [| storage :> obj; historyKey :> obj; historyIndexKey :> obj |]
            |> jsState

        Assert.Equal<string list>([ "one"; "two" ], items)
        Assert.Equal(1, index)

        storage.SetRaw(historyIndexKey, "-5")
        let _, index2 =
            invokeHistory
                engine
                "loadHistoryState"
                [| storage :> obj; historyKey :> obj; historyIndexKey :> obj |]
            |> jsState

        Assert.Equal(0, index2)

    [<Fact>]
    let ``addHistoryEntry trims and deduplicates`` () =
        let storage = FakeStorage()
        let engine = loadHistoryEngine storage

        let items, index =
            invokeHistory
                engine
                "addHistoryEntry"
                [| storage :> obj
                   historyKey :> obj
                   historyIndexKey :> obj
                   "  hello  " :> obj |]
            |> jsState

        Assert.Equal<string list>([ "hello" ], items)
        Assert.Equal(0, index)

        let items2, index2 =
            invokeHistory
                engine
                "addHistoryEntry"
                [| storage :> obj
                   historyKey :> obj
                   historyIndexKey :> obj
                   "hello" :> obj |]
            |> jsState

        Assert.Equal<string list>([ "hello" ], items2)
        Assert.Equal(0, index2)

        let items3, index3 =
            invokeHistory
                engine
                "addHistoryEntry"
                [| storage :> obj
                   historyKey :> obj
                   historyIndexKey :> obj
                   "second" :> obj |]
            |> jsState

        Assert.Equal<string list>([ "hello"; "second" ], items3)
        Assert.Equal(1, index3)
