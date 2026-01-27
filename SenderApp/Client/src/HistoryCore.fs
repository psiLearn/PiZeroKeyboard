module SenderApp.Client.HistoryCore

open System

type HistoryItem =
    { text: string
      timestamp: float option }

type HistoryState =
    { items: HistoryItem list
      index: int }

let maxHistoryItems = 50

let clampIndex (index: int) (maxIndex: int) : int =
    if maxIndex < 0 then 0
    else min (max index 0) maxIndex

let normalizeText (text: string) : string option =
    if isNull text then
        None
    else
        let trimmed = text.Trim()
        if String.IsNullOrWhiteSpace trimmed then None else Some trimmed

let private lastIndex (items: HistoryItem list) : int =
    if items.IsEmpty then 0 else items.Length - 1

let private pruneHistory (items: HistoryItem list) : HistoryItem list =
    if items.Length > maxHistoryItems then
        items |> List.skip (items.Length - maxHistoryItems)
    else
        items

let addEntry (now: unit -> float) (items: HistoryItem list) (trimmed: string) : HistoryState =
    let lastText =
        items
        |> List.tryLast
        |> Option.map (fun item -> item.text)
        |> Option.defaultValue ""

    let updated =
        if items.IsEmpty || lastText <> trimmed then
            let newItem = { text = trimmed; timestamp = Some (now()) }
            items @ [ newItem ] |> pruneHistory
        else
            items

    { items = updated; index = lastIndex updated }

let movePrev (index: int) (items: HistoryItem list) : int =
    if items.IsEmpty then 0
    else clampIndex (index - 1) (items.Length - 1)

let moveNext (index: int) (items: HistoryItem list) : int =
    if items.IsEmpty then 0
    else clampIndex (index + 1) (items.Length - 1)
