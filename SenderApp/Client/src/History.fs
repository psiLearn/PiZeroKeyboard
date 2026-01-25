namespace SenderApp.Client

open System
open Fable.Core
open Fable.Core.JsInterop

// ============================================================================
// INTEROP TYPES
// ============================================================================

/// Type definitions for JavaScript interop
[<Global>]
type JSON =
    static member parse(json: string): 'a = jsNative
    static member stringify(obj: 'a): string = jsNative

[<Global>]
type Storage =
    [<Emit "$0.getItem($1)">]
    member this.getItem(key: string): string option = jsNative
    [<Emit "$0.setItem($1, $2)">]
    member this.setItem(key: string, value: string): unit = jsNative

[<Emit("localStorage")>]
let localStorage: Storage = jsNative

// ============================================================================
// DOMAIN TYPES
// ============================================================================

/// A single history item with text and timestamp
type HistoryItem =
    { text: string
      timestamp: float option }

/// Complete history state with items and current index
type HistoryState =
    { items: HistoryItem list
      index: int }

// ============================================================================
// SAFE STORAGE OPERATIONS
// ============================================================================

module private StorageOps =
    /// Safely retrieve an item from storage, handling errors gracefully
    let getItem (storage: Storage) (key: string) : string option =
        if isNull storage || String.IsNullOrEmpty key then
            None
        else
            try
                storage.getItem key
            with _ -> None

    /// Safely store an item, ignoring errors (quota exceeded, private mode, etc.)
    let setItem (storage: Storage) (key: string) (value: string) : unit =
        if isNull storage || String.IsNullOrEmpty key then
            ()
        else
            try
                storage.setItem (key, value)
            with _ -> ()

// ============================================================================
// JSON PARSING AND FORMATTING
// ============================================================================

module private Parsing =
    /// Parse items from JSON, supporting both old string and new object formats
    let parseItems (raw: string option) : HistoryItem list =
        match raw with
        | None -> []
        | Some json ->
            try
                let parsed: obj[] = JSON.parse json
                parsed
                |> Array.toList
                |> List.choose (fun item ->
                    if isNull item then
                        None
                    else
                        try
                            // Check if it's old string format
                            if typeof<string> = item.GetType() then
                                Some { text = string item; timestamp = None }
                            // Check for new object format
                            elif not (isNull item?text) then
                                let text: string = unbox item?text
                                let timestamp: float option =
                                    if isNull item?timestamp then
                                        None
                                    else
                                        try
                                            float item?timestamp |> Some
                                        with _ -> None
                                Some { text = text; timestamp = timestamp }
                            else
                                None
                        with _ -> None)
            with _ -> []

    /// Format a history item for display in UI
    let formatPreview (item: HistoryItem) : string =
        let preview =
            if item.text.Length > 30 then
                item.text.[0..29] + "…"
            else
                item.text

        match item.timestamp with
        | None -> preview
        | Some ts ->
            try
                let date = System.DateTime(int64 ts)
                let timeStr = sprintf "%02d:%02d:%02d | " date.Hour date.Minute date.Second
                timeStr + preview
            with _ -> preview

// ============================================================================
// CORE OPERATIONS
// ============================================================================

module History =
    
    // Constants
    let historyKey = "linuxkey-history"
    let historyIndexKey = "linuxkey-history-index"

    // ========================================================================
    // READING
    // ========================================================================

    /// Read all history items from storage
    let readHistory () : HistoryItem list =
        StorageOps.getItem localStorage historyKey
        |> Parsing.parseItems

    /// Read the current history index from storage
    let readHistoryIndex (maxIndex: int) : int =
        if maxIndex < 0 then 0
        else
            match StorageOps.getItem localStorage historyIndexKey with
            | None | Some "" -> maxIndex
            | Some raw ->
                try
                    let index = int raw
                    Math.Min(Math.Max(index, 0), maxIndex)
                with _ -> Math.Min(Math.Max(0, 0), maxIndex)

    // ========================================================================
    // WRITING
    // ========================================================================

    /// Write history items to storage as JSON
    let writeHistory (items: HistoryItem list) : unit =
        try
            let json =
                items
                |> List.map (fun item ->
                    let ts =
                        match item.timestamp with
                        | None -> "null"
                        | Some t -> t.ToString()
                    sprintf
                        "{\"text\":%s,\"timestamp\":%s}"
                        (JSON.stringify item.text)
                        ts)
                |> String.concat ","
            StorageOps.setItem localStorage historyKey ("[" + json + "]")
        with _ -> ()

    /// Write the current history index to storage
    let writeHistoryIndex (index: int) : unit =
        StorageOps.setItem localStorage historyIndexKey (string index)

    // ========================================================================
    // INDEX MANAGEMENT
    // ========================================================================

    /// Clamp index to valid range [0, maxIndex]
    let clampIndex (index: int) (maxIndex: int) : int =
        if maxIndex < 0 then 0
        else Math.Min(Math.Max(index, 0), maxIndex)

    // ========================================================================
    // STATE MANAGEMENT
    // ========================================================================

    /// Load complete history state (items + current index)
    let loadHistoryState () : HistoryState =
        let items = readHistory ()
        if items.IsEmpty then
            { items = []; index = 0 }
        else
            let maxIndex = items.Length - 1
            let index = readHistoryIndex maxIndex
            { items = items; index = index }

    /// Add a new history entry, avoiding consecutive duplicates
    let addHistoryEntry (text: string) : HistoryState =
        let trimmed = (text |> string).Trim()
        if String.IsNullOrEmpty trimmed then
            loadHistoryState ()
        else
            let items = readHistory ()
            let shouldAdd =
                items.IsEmpty
                || (match items |> List.tryLast with
                    | Some lastItem -> lastItem.text <> trimmed
                    | None -> true)

            if shouldAdd then
                let newItem =
                    { text = trimmed
                      timestamp = Some (DateTime.Now.GetTime()) }
                let newItems = items @ [ newItem ]
                writeHistory newItems
                let newIndex = newItems.Length - 1
                writeHistoryIndex newIndex
                { items = newItems; index = newIndex }
            else
                { items = items
                  index = if items.IsEmpty then 0 else items.Length - 1 }

    // ========================================================================
    // FORMATTING
    // ========================================================================

    /// Format a history item for display preview
    let formatHistoryPreview (item: HistoryItem) : string =
        Parsing.formatPreview item

// ============================================================================
// JAVASCRIPT INTEROP EXPORTS
// ============================================================================

/// Expose History module functions as global object for JavaScript
[<Global>]
let LinuxKeyHistory : obj =
    jsOptions (fun o ->
        o?readHistory <- fun () -> History.readHistory ()
        o?writeHistory <- fun (items: HistoryItem list) -> History.writeHistory items
        o?readHistoryIndex <- fun (maxIndex: int) -> History.readHistoryIndex maxIndex
        o?writeHistoryIndex <- fun (index: int) -> History.writeHistoryIndex index
        o?loadHistoryState <- fun () -> History.loadHistoryState ()
        o?addHistoryEntry <- fun (text: string) -> History.addHistoryEntry text
        o?clampIndex <- fun (index: int) (maxIndex: int) -> History.clampIndex index maxIndex
        o?formatHistoryPreview <- fun (item: HistoryItem) -> History.formatHistoryPreview item)

