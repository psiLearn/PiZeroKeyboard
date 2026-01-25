namespace SenderApp.Tests

open System
open System.Collections.Generic
open System.IO
open Jint
open Jint.Native
open Jint.Runtime
open Jint.Runtime.Interop
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

    type EventStub() =
        member _.preventDefault() = ()

    type WebSocketStub(url: string) =
        member val onmessage: obj = null with get, set
        member val onerror: obj = null with get, set
        member val onclose: obj = null with get, set
        member _.close() = ()

    type LocationStub() =
        member val protocol = "http:" with get, set
        member val host = "localhost" with get, set

    type WindowStub(engine: Engine) =
        let ctor = TypeReference.CreateTypeReference(engine, typeof<WebSocketStub>)
        member val location = LocationStub() with get, set
        member val WebSocket = ctor with get, set
        member _.setTimeout(_: obj, _: obj) = 0

    type DomElement(engine: Engine, id: string, ?closestTarget: obj) =
        let handlers = Dictionary<string, JsValue>()
        member val id = id with get, set
        member val value = "" with get, set
        member val disabled = false with get, set
        member val className = "" with get, set
        member val selectionStart = 0 with get, set
        member val selectionEnd = 0 with get, set
        member _.setAttribute(_: string, _: obj) = ()
        member _.getAttribute(_: string) = null
        member _.setSelectionRange(_: int, _: int) = ()
        member _.focus() = ()
        member _.addEventListener(name: string, callback: JsValue) =
            handlers.[name] <- callback
        member _.click() =
            match handlers.TryGetValue("click") with
            | true, callback ->
                engine.Invoke(callback, JsValue.Undefined, [| EventStub() |]) |> ignore
            | _ -> ()
        member _.closest(selector: string) =
            if selector = "form" then
                defaultArg closestTarget null
            else
                null

    type DocumentStub(engine: Engine, elements: IDictionary<string, DomElement>) =
        let mutable domReady: JsValue option = None
        member _.addEventListener(name: string, callback: JsValue) =
            if name = "DOMContentLoaded" then
                domReady <- Some callback
        member _.getElementById(id: string) =
            match elements.TryGetValue id with
            | true, element -> element
            | _ -> null
        member _.querySelectorAll(_: string) =
            engine.Realm.Intrinsics.Array.Construct(Arguments.Empty)
        member _.TriggerDOMContentLoaded() =
            match domReady with
            | Some callback -> engine.Invoke(callback, JsValue.Undefined, [||]) |> ignore
            | None -> ()

    let loadHistoryEngine (storage: FakeStorage) =
        let engine = new Engine()
        let scriptPath =
            Path.Combine(__SOURCE_DIRECTORY__, "..", "SenderApp", "wwwroot", "history.js")
        let script = File.ReadAllText(scriptPath)
        engine.Execute(script) |> ignore
        engine.SetValue("localStorage", storage) |> ignore
        engine

    let loadSenderDomEngine (storage: FakeStorage) =
        let engine = new Engine()
        engine.SetValue("localStorage", storage) |> ignore
        let elements = Dictionary<string, DomElement>()
        let form = DomElement(engine, "form")
        let textarea = DomElement(engine, "text", form)
        let back = DomElement(engine, "history-back")
        let forward = DomElement(engine, "history-forward")
        elements.["form"] <- form
        elements.["text"] <- textarea
        elements.["history-back"] <- back
        elements.["history-forward"] <- forward

        let document = DocumentStub(engine, elements)
        let window = WindowStub(engine)
        engine.SetValue("document", document) |> ignore
        engine.SetValue("window", window) |> ignore
        engine.SetValue("WebSocket", window.WebSocket) |> ignore
        let historyPath =
            Path.Combine(__SOURCE_DIRECTORY__, "..", "SenderApp", "wwwroot", "history.js")
        let senderPath =
            Path.Combine(__SOURCE_DIRECTORY__, "..", "SenderApp", "wwwroot", "sender.js")
        engine.Execute(File.ReadAllText(historyPath)) |> ignore
        engine.Execute(File.ReadAllText(senderPath)) |> ignore
        engine, document

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

    [<Fact>]
    let ``history back button restores previous text`` () =
        let storage = FakeStorage()
        storage.SetRaw(historyKey, "[\"first\",\"second\"]")
        storage.SetRaw(historyIndexKey, "1")
        let (engine, document) = loadSenderDomEngine storage

        document.TriggerDOMContentLoaded()
        engine.Execute("elements['history-back'].click();") |> ignore

        let value = engine.Evaluate("elements['text'].value").ToString()
        Assert.Equal("first", value)
        Assert.Equal("0", storage.getItem(historyIndexKey))
