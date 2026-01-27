module SenderApp.Client.Sender

open System
open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.JS
open SenderApp.Client.History
open SenderApp.Client.HistoryCore

[<Emit("document")>]
let document: obj = jsNative

[<Emit("globalThis")>]
let globalThis: obj = jsNative

[<Emit("document.getElementById($0)")>]
let getElementById (id: string): obj = jsNative

[<Emit("document.querySelectorAll($0)")>]
let querySelectorAll (selector: string): obj = jsNative

[<Emit("document.createElement($0)")>]
let createElement (tag: string): obj = jsNative

[<Emit("new WebSocket($0)")>]
let newWebSocket (url: string): obj = jsNative

let mutable autoRetryEnabled = false
let mutable autoRetryTimer: obj option = None
let mutable retryCountdownTimer: obj option = None
let mutable nextRetryCountdown = 0

let mutable historyItems: HistoryItem list = []
let mutable historyIndex = 0

let setDot (element: obj) (baseClass: string) (text: string) (cssClass: string) =
    if not (isNull element) then
        let cls = if String.IsNullOrWhiteSpace cssClass then baseClass else sprintf "%s %s" baseClass cssClass
        element?className <- cls
        element?setAttribute("aria-label", text)

let tryGetString (data: obj) (prop: string) (fallback: string) =
    try
        let value: obj = data?(prop)
        if isNull value then fallback else string value
    with _ -> fallback

let applyStatus (data: obj) =
    let statusEl = getElementById "usb-status"
    let capsEl = getElementById "caps-status"
    let text = tryGetString data "text" "Raspberry Pi USB: unknown"
    let cssClass = tryGetString data "cssClass" "unknown"
    let capsText = tryGetString data "capsText" "Caps Lock: unknown"
    let capsCssClass = tryGetString data "capsCssClass" "unknown"
    setDot statusEl "usb-dot" text cssClass
    setDot capsEl "caps-dot" capsText capsCssClass

[<Emit("fetch('/status', { cache: 'no-store' })\
    .then(resp => resp.ok ? resp.json() : Promise.reject(new Error(`HTTP ${resp.status}`)))\
    .then(data => $0(data))\
    .catch(() => $1())\
    .finally(() => { if ($2) $2.disabled = false; })")>]
let fetchStatus (onSuccess: obj -> unit) (onFail: unit -> unit) (btn: obj) : unit = jsNative

let refreshStatus () =
    let refreshBtn = getElementById "refresh-status"
    if not (isNull refreshBtn) then
        refreshBtn?disabled <- true
    let onFail () =
        applyStatus (createObj [ "text" ==> "Raspberry Pi USB: unknown (refresh failed)"; "cssClass" ==> "unknown" ])
    fetchStatus applyStatus onFail refreshBtn

let updateRetryCountdown () =
    let el = getElementById "retry-countdown"
    if not (isNull el) then
        let text = if nextRetryCountdown > 0 then sprintf "Retrying in %ds…" nextRetryCountdown else ""
        el?textContent <- text

let startRetryCountdown (seconds: int) =
    nextRetryCountdown <- seconds
    match retryCountdownTimer with
    | Some id -> globalThis?clearInterval(id)
    | None -> ()
    updateRetryCountdown()
    let timer = globalThis?setInterval((fun () ->
        nextRetryCountdown <- nextRetryCountdown - 1
        updateRetryCountdown()
        if nextRetryCountdown <= 0 then
            match retryCountdownTimer with
            | Some id -> globalThis?clearInterval(id)
            | None -> ()
            retryCountdownTimer <- None), 1000)
    retryCountdownTimer <- Some timer

let initStatusRefresh () =
    let refreshBtn = getElementById "refresh-status"
    if not (isNull refreshBtn) then
        refreshBtn?addEventListener("click", fun (event: obj) ->
            event?preventDefault()
            refreshStatus())

let initAutoRetry () =
    let checkbox = getElementById "auto-retry"
    if not (isNull checkbox) then
        checkbox?addEventListener("change", fun (_: obj) ->
            autoRetryEnabled <- unbox<bool> checkbox?``checked``
            if autoRetryEnabled then
                match autoRetryTimer with
                | Some id -> globalThis?clearInterval(id)
                | None -> ()
                let timer = globalThis?setInterval((fun () ->
                    let statusEl = getElementById "usb-status"
                    let isConnected =
                        if isNull statusEl then false
                        else
                            try
                                let classList: obj = statusEl?classList
                                classList?contains("connected") |> unbox<bool>
                            with _ -> false
                    if not isConnected then
                        refreshStatus()
                        startRetryCountdown 5), 5000)
                autoRetryTimer <- Some timer
            else
                match autoRetryTimer with
                | Some id -> globalThis?clearInterval(id)
                | None -> ()
                autoRetryTimer <- None
                match retryCountdownTimer with
                | Some id -> globalThis?clearInterval(id)
                | None -> ()
                retryCountdownTimer <- None
                nextRetryCountdown <- 0
                updateRetryCountdown())

let rec setupWebSocketConnection () =
    let hasWebSocket =
        try not (isNull globalThis?WebSocket)
        with _ -> false
    if hasWebSocket then
        try
            let scheme = if string (globalThis?location?protocol) = "https:" then "wss:" else "ws:"
            let host = string (globalThis?location?host)
            let url = sprintf "%s//%s/status/ws" scheme host
            let socket = newWebSocket url
            socket?onmessage <- (fun (event: obj) ->
                try
                    let data = JS.JSON.parse (string event?data)
                    applyStatus data
                with _ ->
                    applyStatus (createObj [ "text" ==> "Raspberry Pi USB: unknown"; "cssClass" ==> "unknown" ]))
            socket?onerror <- (fun (_: obj) -> socket?close())
            socket?onclose <- (fun (_: obj) ->
                globalThis?setTimeout((fun () -> setupWebSocketConnection()), 3000) |> ignore)
        with _ ->
            globalThis?setTimeout((fun () -> setupWebSocketConnection()), 3000) |> ignore

let insertToken (token: string) =
    let textarea = getElementById "text"
    if not (isNull textarea) && not (String.IsNullOrWhiteSpace token) then
        let start = unbox<int> textarea?selectionStart
        let finish = unbox<int> textarea?selectionEnd
        let currentValue = string textarea?value
        let newValue = currentValue.Substring(0, start) + token + currentValue.Substring(finish)
        textarea?value <- newValue
        let caret = start + token.Length
        textarea?setSelectionRange(caret, caret)
        textarea?focus()

let initTokenButtons () =
    let buttons = querySelectorAll "[data-token]"
    if not (isNull buttons) then
        let length = unbox<int> buttons?length
        for i = 0 to length - 1 do
            let button = buttons?item(i)
            if not (isNull button) then
                button?addEventListener("click", fun (event: obj) ->
                    event?preventDefault()
                    let token = string button?dataset?token
                    insertToken token)

let updateHistoryButtons () =
    let historyBack = getElementById "history-prev"
    let historyForward = getElementById "history-next"
    if not (isNull historyBack) then
        historyBack?disabled <- historyIndex <= 0
    if not (isNull historyForward) then
        historyForward?disabled <- historyIndex >= historyItems.Length - 1

let applyHistory () =
    let textarea = getElementById "text"
    if not (isNull textarea) && historyIndex >= 0 && historyIndex < historyItems.Length then
        let item = historyItems.[historyIndex]
        let value = item.text
        textarea?value <- value
        let caret = value.Length
        textarea?setSelectionRange(caret, caret)
        textarea?focus()

let rec renderHistoryList () =
    let historyList: obj = getElementById "history-list"
    if not (isNull historyList) then
        historyList?innerHTML <- ""
        if historyItems.IsEmpty then
            let empty = createElement "div"
            empty?className <- "history-empty"
            empty?textContent <- "No history yet."
            historyList?appendChild(empty) |> ignore
        else
            historyItems
            |> List.iteri (fun index item ->
                let button: obj = createElement "button"
                button?``type`` <- "button"
                button?className <- "history-item"
                if index = historyIndex then
                    let classList: obj = button?classList
                    classList?add("active") |> ignore
                button?textContent <- formatHistoryPreview item
                let onClick (event: obj) =
                    event?preventDefault()
                    historyIndex <- index
                    writeHistoryIndex historyIndex
                    applyHistory()
                    updateHistoryButtons()
                    renderHistoryList()
                    let historyClassList: obj = historyList?classList
                    historyClassList?add("hidden") |> ignore
                button?addEventListener("click", onClick)
                historyList?appendChild(button) |> ignore)

let refreshHistoryState () =
    let state = loadHistoryState()
    historyItems <- state.items
    historyIndex <- state.index
    updateHistoryButtons()
    renderHistoryList()

let initHistoryNavigation () =
    let historyBack = getElementById "history-prev"
    let historyForward = getElementById "history-next"
    if (not (isNull historyBack)) || (not (isNull historyForward)) then
        refreshHistoryState()
        if not (isNull historyBack) then
            historyBack?addEventListener("click", fun (event: obj) ->
                event?preventDefault()
                if not historyItems.IsEmpty then
                    historyIndex <- movePrev historyIndex historyItems
                    writeHistoryIndex historyIndex
                    applyHistory()
                    updateHistoryButtons()
                    renderHistoryList())
        if not (isNull historyForward) then
            historyForward?addEventListener("click", fun (event: obj) ->
                event?preventDefault()
                if not historyItems.IsEmpty then
                    historyIndex <- moveNext historyIndex historyItems
                    writeHistoryIndex historyIndex
                    applyHistory()
                    updateHistoryButtons()
                    renderHistoryList())

let initHistoryToggle () =
    let historyToggle: obj = getElementById "history-toggle"
    let historyList: obj = getElementById "history-list"
    if not (isNull historyToggle) && not (isNull historyList) then
        historyToggle?addEventListener("click", fun (event: obj) ->
            event?preventDefault()
            renderHistoryList()
            let classList: obj = historyList?classList
            classList?toggle("hidden") |> ignore)

let initKeyboardShortcuts () =
    let textarea = getElementById "text"
    let form = if isNull textarea then null else textarea?closest("form")
    if not (isNull textarea) then
        textarea?addEventListener("keydown", fun (event: obj) ->
            let ctrlKey = unbox<bool> event?ctrlKey
            let metaKey = unbox<bool> event?metaKey
            let key = string event?key
            if (ctrlKey || metaKey) && key = "Enter" then
                event?preventDefault()
                let sendBtn = getElementById "send-text"
                if not (isNull sendBtn) && not (unbox<bool> sendBtn?disabled) then
                    form?submit())

let initFormSubmitHandler () =
    let textarea = getElementById "text"
    let form = if isNull textarea then null else textarea?closest("form")
    let statusLine: obj = getElementById "status-line"
    if not (isNull form) then
        form?addEventListener("submit", fun (_: obj) ->
            if not (isNull statusLine) then
                statusLine?textContent <- "Sending..."
                let statusClassList: obj = statusLine?classList
                statusClassList?add("sending") |> ignore
                statusClassList?remove("sent") |> ignore
            let privateSendCheckbox = getElementById "private-send"
            let isPrivateSend = if isNull privateSendCheckbox then false else unbox<bool> privateSendCheckbox?``checked``
            if not isPrivateSend then
                let text = string textarea?value
                let state = addHistoryEntry text
                historyItems <- state.items
                historyIndex <- state.index
                updateHistoryButtons()
                renderHistoryList()
            globalThis?setTimeout((fun () ->
                if not (isNull statusLine) then
                    statusLine?textContent <- "Sent ✓"
                    let statusClassList: obj = statusLine?classList
                    statusClassList?remove("sending") |> ignore
                    statusClassList?add("sent") |> ignore
                    globalThis?setTimeout((fun () ->
                        statusLine?textContent <- "Ready"
                        let statusClassList: obj = statusLine?classList
                        statusClassList?remove("sent") |> ignore), 3000) |> ignore), 200) |> ignore)

let init () =
    initStatusRefresh()
    initAutoRetry()
    setupWebSocketConnection()
    initTokenButtons()
    initHistoryNavigation()
    initHistoryToggle()
    initKeyboardShortcuts()
    initFormSubmitHandler()
    refreshStatus()

let start () =
    let state = string document?readyState
    if state = "loading" then
        document?addEventListener("DOMContentLoaded", fun _ -> init())
    else
        init()

let () = start()
