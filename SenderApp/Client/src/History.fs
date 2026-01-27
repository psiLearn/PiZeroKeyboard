module SenderApp.Client.History

open System
open Fable.Core
open Fable.Core.JsInterop
open SenderApp.Client.HistoryCore

// ============================================================================
// JS INTEROP
// ============================================================================

[<Emit("window.localStorage")>]
let localStorage: obj = jsNative

[<Emit("typeof $0")>]
let jsTypeof (value: obj): string = jsNative

// ============================================================================
// STORAGE HELPERS
// ============================================================================

module private StorageOps =
    let tryGetItem (key: string) : string option =
        if String.IsNullOrWhiteSpace key then
            None
        else
            try
                let value: obj = localStorage?getItem(key)
                if isNull value then None else Some (string value)
            with _ -> None

    let trySetItem (key: string) (value: string) : unit =
        if String.IsNullOrWhiteSpace key then
            ()
        else
            try
                localStorage?setItem(key, value) |> ignore
            with _ -> ()

// ============================================================================
// HISTORY LOGIC
// ============================================================================

let historyKey = "linuxkey-history"
let historyIndexKey = "linuxkey-history-index"

let private parseItems (raw: string option) : HistoryItem list =
    match raw with
    | None -> []
    | Some json ->
        try
            let parsed: obj = Fable.Core.JS.JSON.parse json
            let items: obj[] = unbox parsed
            items
            |> Microsoft.FSharp.Collections.Array.toList
            |> List.choose (fun item ->
                if isNull item then
                    None
                elif jsTypeof item = "string" then
                    Some { text = string item; timestamp = Some (Fable.Core.JS.Constructors.Date.now()) }
                else
                    let textObj: obj = item?text
                    if isNull textObj then
                        None
                    else
                        let text = string textObj
                        let tsObj: obj = item?timestamp
                        let timestamp =
                            if isNull tsObj then None
                            else
                                try Some (unbox<float> tsObj) with _ -> None
                        Some { text = text; timestamp = timestamp })
        with _ -> []

let private toJsItem (item: HistoryItem) : obj =
    let ts: obj =
        match item.timestamp with
        | None -> null
        | Some value -> box value
    createObj [ "text" ==> item.text; "timestamp" ==> ts ]

let readHistory () : HistoryItem list =
    StorageOps.tryGetItem historyKey
    |> parseItems

let writeHistory (items: HistoryItem list) : unit =
    let jsItems = items |> List.map toJsItem |> List.toArray
    try
        let json = Fable.Core.JS.JSON.stringify jsItems
        StorageOps.trySetItem historyKey json
    with _ -> ()

let readHistoryIndex (maxIndex: int) : int =
    if maxIndex < 0 then 0
    else
        match StorageOps.tryGetItem historyIndexKey with
        | None | Some "" -> maxIndex
        | Some raw ->
            try
                clampIndex (int raw) maxIndex
            with _ -> clampIndex 0 maxIndex

let writeHistoryIndex (index: int) : unit =
    StorageOps.trySetItem historyIndexKey (string index)

let loadHistoryState () : HistoryState =
    let items = readHistory ()
    if items.IsEmpty then
        { items = []; index = 0 }
    else
        let maxIndex = items.Length - 1
        let index = readHistoryIndex maxIndex
        { items = items; index = index }

let addHistoryEntry (text: string) : HistoryState =
    match normalizeText text with
    | None -> loadHistoryState ()
    | Some trimmed ->
        let items = readHistory ()
        let state = addEntry (fun () -> Fable.Core.JS.Constructors.Date.now()) items trimmed
        if state.items <> items then
            writeHistory state.items
        writeHistoryIndex state.index
        state

let formatHistoryPreview (item: HistoryItem) : string =
    let preview =
        if item.text.Length > 30 then
            item.text.Substring(0, 30) + "…"
        else
            item.text

    match item.timestamp with
    | None -> preview
    | Some ts ->
        try
            let date: obj = Fable.Core.JS.Constructors.Date.Create(ts)
            let hours = int (date?getHours())
            let minutes = int (date?getMinutes())
            let seconds = int (date?getSeconds())
            sprintf "%02d:%02d:%02d | %s" hours minutes seconds preview
        with _ -> preview

// ============================================================================
// GLOBAL EXPORT (OPTIONAL COMPATIBILITY)
// ============================================================================

[<Global>]
let LinuxKeyHistory : obj =
    jsOptions (fun o ->
        o?readHistory <- fun () -> readHistory ()
        o?writeHistory <- fun (items: HistoryItem list) -> writeHistory items
        o?readHistoryIndex <- fun (maxIndex: int) -> readHistoryIndex maxIndex
        o?writeHistoryIndex <- fun (index: int) -> writeHistoryIndex index
        o?loadHistoryState <- fun () -> loadHistoryState ()
        o?addHistoryEntry <- fun (text: string) -> addHistoryEntry text
        o?clampIndex <- fun (index: int) (maxIndex: int) -> clampIndex index maxIndex
        o?formatHistoryPreview <- fun (item: HistoryItem) -> formatHistoryPreview item)
