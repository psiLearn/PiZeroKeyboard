namespace SenderApp.Client

open Fable.Core
open Fable.Core.JsInterop

[<Global>]
type JSON =
    static member parse(json: string): 'a = jsNative
    static member stringify(obj: 'a): string = jsNative

[<Global>]
type LocalStorage =
    [<Emit "$0.getItem($1)">]
    member this.getItem(key: string): string option = jsNative
    [<Emit "$0.setItem($1, $2)">]
    member this.setItem(key: string, value: string): unit = jsNative

[<Emit("localStorage")>]
let localStorage: LocalStorage = jsNative

module History =
    
    let historyKey = "linuxkey-history"
    let historyIndexKey = "linuxkey-history-index"

    let loadHistoryState () : obj[] =
        try
            let json = localStorage.getItem(historyKey)
            match json with
            | None | Some "" -> [||]
            | Some json -> JSON.parse(json)
        with _ ->
            [||]

    let getIndex () : int =
        try
            let json = localStorage.getItem(historyIndexKey)
            match json with
            | None | Some "" -> 0
            | Some json -> JSON.parse(json)
        with _ ->
            0

    let saveItems (items: obj[]) : unit =
        localStorage.setItem(historyKey, JSON.stringify(items))

    let saveIndex (index: int) : unit =
        localStorage.setItem(historyIndexKey, JSON.stringify(index))

    let addEntry (text: string) : obj[] =
        let items = loadHistoryState()
        let newItems =
            if items.Length > 0 then
                let lastItem = items.[items.Length - 1]
                try
                    let lastText: string = unbox (lastItem?text)
                    if lastText = text then items 
                    else 
                        let newEntry: obj = createObj ["text" ==> text; "timestamp" ==> System.DateTime.Now.Millisecond]
                        Array.append items [| newEntry |]
                with _ ->
                    let newEntry: obj = createObj ["text" ==> text; "timestamp" ==> System.DateTime.Now.Millisecond]
                    Array.append items [| newEntry |]
            else
                let newEntry: obj = createObj ["text" ==> text; "timestamp" ==> System.DateTime.Now.Millisecond]
                [| newEntry |]
        saveItems(newItems)
        newItems

