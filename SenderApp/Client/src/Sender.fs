namespace SenderApp.Client

open Fable.Core
open Fable.Core.JsInterop

[<Global>]
type Document =
    static member getElementById(id: string): obj = jsNative
    static member querySelectorAll(selector: string): obj = jsNative
    static member readyState: string = jsNative
    static member addEventListener(event: string, handler: obj -> unit): unit = jsNative

[<Global>]
let document: Document = jsNative

[<Global>]
type GlobalThis =
    static member location: obj = jsNative
    static member navigator: obj = jsNative
    static member setTimeout(fn: unit -> unit, ms: float): float = jsNative
    static member setInterval(fn: unit -> unit, ms: float): float = jsNative
    static member clearInterval(id: float): unit = jsNative

[<Global>]
let globalThis: GlobalThis = jsNative

module Sender =
    open History
    
    let mutable autoRetryEnabled = false
    let mutable autoRetryTimer: float option = None
    let mutable retryCountdownTimer: float option = None
    let mutable nextRetryCountdown = 0
    
    let mutable historyItems: obj[] = [||]
    let mutable historyIndex = 0

    // ===== Status Dot Management =====
    let setDot (element: obj) baseClass text cssClass =
        if not (isNull element) then
            let className = if String.length cssClass > 0 then sprintf "%s %s" baseClass cssClass else baseClass
            element?className <- className
            element?setAttribute("title", text)
            element?setAttribute("aria-label", text)

    let applyStatus (data: obj) =
        let statusEl = document.getElementById "usb-status"
        let capsEl = document.getElementById "caps-status"
        
        let text = 
            try
                let t = data?text
                if isNull t then "Raspberry Pi USB: unknown" else unbox<string> t
            with _ -> "Raspberry Pi USB: unknown"
        
        let cssClass = 
            try
                let c = data?cssClass
                if isNull c then "unknown" else unbox<string> c
            with _ -> "unknown"
        
        let capsText = 
            try
                let t = data?capsText
                if isNull t then "Caps Lock: unknown" else unbox<string> t
            with _ -> "Caps Lock: unknown"
        
        let capsCssClass = 
            try
                let c = data?capsCssClass
                if isNull c then "unknown" else unbox<string> c
            with _ -> "unknown"
        
        setDot statusEl "usb-dot" text cssClass
        setDot capsEl "caps-dot" capsText capsCssClass

    let refreshStatus () =
        let refreshBtn = document.getElementById "refresh-status"
        
        if not (isNull refreshBtn) then
            refreshBtn?disabled <- true
        
        [<Emit("fetch('/status', { cache: 'no-store' }).then(resp => { if (resp.ok) return resp.json(); throw new Error('HTTP ' + resp.status); }).then(data => { $data = data; }).catch(err => { $data = null; }).finally(() => { if ($btn) $btn.disabled = false; })")>]
        let performFetch (btn: obj) (data: obj byref): unit = jsNative
        
        let mutable data = null
        performFetch refreshBtn &data
        
        if not (isNull data) then
            applyStatus data
        else
            applyStatus (createObj ["text" ==> "Raspberry Pi USB: unknown (refresh failed)"; "cssClass" ==> "unknown"])

    // ===== Auto-Retry Countdown =====
    let updateRetryCountdown () =
        let el = document.getElementById "retry-countdown"
        if not (isNull el) then
            let text = if nextRetryCountdown > 0 then sprintf "Retrying in %ds…" nextRetryCountdown else ""
            el?textContent <- text

    let startRetryCountdown seconds =
        nextRetryCountdown <- seconds
        
        match retryCountdownTimer with
        | Some id -> globalThis.clearInterval(id)
        | None -> ()

        updateRetryCountdown()
        
        let timer = globalThis.setInterval((fun () ->
            nextRetryCountdown <- nextRetryCountdown - 1
            updateRetryCountdown()
            if nextRetryCountdown <= 0 then
                match retryCountdownTimer with
                | Some id -> globalThis.clearInterval(id)
                | None -> ()
                retryCountdownTimer <- None), 1000.0)

        retryCountdownTimer <- Some timer

    // ===== Copy Button =====
    let initCopyButton () =
        let btn = document.getElementById "copy-target"
        if not (isNull btn) then
            btn?addEventListener("click", fun (event: obj) ->
                event?preventDefault()
                let display = document.getElementById "target-display"
                if not (isNull display) then
                    let text = unbox<string> (display?textContent)
                    let clipboard = globalThis.navigator?clipboard
                    if not (isNull clipboard) then
                        let promise = clipboard?writeText(text)
                        promise
                            ?``then``(fun _ ->
                                let originalText = btn?textContent
                                btn?textContent <- "✓"
                                globalThis.setTimeout((fun () -> btn?textContent <- originalText), 1500.0) |> ignore)
                            ?``catch``(fun err -> 
                                JS.console.error("Failed to copy:", err)) |> ignore)

    // ===== USB Status & Refresh =====
    let initStatusRefresh () =
        let refreshBtn = document.getElementById "refresh-status"
        if not (isNull refreshBtn) then
            refreshBtn?addEventListener("click", fun (event: obj) ->
                event?preventDefault()
                refreshStatus())

    // ===== Auto-Retry =====
    let initAutoRetry () =
        let checkbox = document.getElementById "auto-retry"
        if not (isNull checkbox) then
            checkbox?addEventListener("change", fun (_: obj) ->
                autoRetryEnabled <- unbox<bool> (checkbox?``checked``)
                
                if autoRetryEnabled then
                    match autoRetryTimer with
                    | Some id -> globalThis.clearInterval(id)
                    | None -> ()

                    let timer = globalThis.setInterval((fun () ->
                        let statusEl = document.getElementById "usb-status"
                        if not (isNull statusEl) then
                            try
                                let hasClass: bool = statusEl?classList.contains("connected")
                                if not hasClass then
                                    refreshStatus()
                                    startRetryCountdown 5
                            with _ -> ()), 5000.0)

                    autoRetryTimer <- Some timer
                else
                    match autoRetryTimer with
                    | Some id -> globalThis.clearInterval(id)
                    | None -> ()
                    
                    match retryCountdownTimer with
                    | Some id -> globalThis.clearInterval(id)
                    | None -> ()
                    
                    nextRetryCountdown <- 0
                    updateRetryCountdown())

    // ===== WebSocket Connection =====
    let rec setupWebSocketConnection () =
        [<Emit("'WebSocket' in globalThis")>]
        let hasWebSocket: bool = jsNative
        
        if hasWebSocket then
            try
                let scheme = if unbox<string> (globalThis.location?protocol) = "https:" then "wss:" else "ws:"
                let host = unbox<string> (globalThis.location?host)
                let url = sprintf "%s//%s/status/ws" scheme host
                
                [<Emit("new WebSocket($0)")>]
                let createSocket (u: string): obj = jsNative
                
                let socket = createSocket url
                
                socket?onmessage <- fun (event: obj) ->
                    try
                        let json = unbox<obj> (event?data)
                        let data: obj = [<Emit("JSON.parse($json)")>] (fun () -> jsNative) ()
                        applyStatus data
                    with _ ->
                        applyStatus (createObj ["text" ==> "Raspberry Pi USB: unknown"; "cssClass" ==> "unknown"])
                
                socket?onerror <- fun (_: obj) ->
                    socket?close()
                
                socket?onclose <- fun (_: obj) ->
                    globalThis.setTimeout((fun () -> setupWebSocketConnection()), 3000.0) |> ignore
            with _ ->
                globalThis.setTimeout((fun () -> setupWebSocketConnection()), 3000.0) |> ignore

    // ===== Token Insertion =====
    let insertToken (token: string) =
        let textarea = document.getElementById "text"
        if not (isNull textarea) && token.Length > 0 then
            let start = unbox<int> (textarea?selectionStart)
            let endSel = unbox<int> (textarea?selectionEnd)
            let currentValue = unbox<string> (textarea?value)
            let newValue = sprintf "%s%s%s" (currentValue.Substring(0, start)) token (currentValue.Substring(endSel))
            
            textarea?value <- newValue
            let caret = start + token.Length
            textarea?setSelectionRange(caret, caret)
            textarea?focus()

    let initTokenButtons () =
        let buttons = document.querySelectorAll("[data-token]")
        if not (isNull buttons) then
            [<Emit("$0.length")>]
            let getLength (obj: obj): int = jsNative
            
            for i = 0 to (getLength buttons) - 1 do
                [<Emit("$0.item($1)")>]
                let getItem (obj: obj) (idx: int): obj = jsNative
                
                let button = getItem buttons i
                if not (isNull button) then
                    button?addEventListener("click", fun (event: obj) ->
                        event?preventDefault()
                        let token = unbox<string> (button?dataset?token)
                        insertToken token)

    // ===== History Navigation =====
    let loadHistoryState () =
        let state = loadHistoryState()
        historyItems <- state
        historyIndex <- getIndex()
        (historyItems, historyIndex)

    let persistHistoryIndex () =
        saveIndex historyIndex

    let updateHistoryButtons () =
        let historyBack = document.getElementById "history-back"
        let historyForward = document.getElementById "history-forward"
        
        if not (isNull historyBack) then
            historyBack?disabled <- historyIndex <= 0
        if not (isNull historyForward) then
            historyForward?disabled <- historyIndex >= historyItems.Length - 1

    let applyHistory () =
        let textarea = document.getElementById "text"
        if not (isNull textarea) && historyIndex >= 0 && historyIndex < historyItems.Length then
            let item = historyItems.[historyIndex]
            let value =
                try
                    let text = unbox<string> (item?text)
                    text
                with _ ->
                    ""
            
            textarea?value <- value
            let caret = value.Length
            textarea?setSelectionRange(caret, caret)
            textarea?focus()

    let initHistoryNavigation () =
        let historyBack = document.getElementById "history-back"
        let historyForward = document.getElementById "history-forward"
        
        if (not (isNull historyBack)) || (not (isNull historyForward)) then
            let (items, idx) = loadHistoryState()
            historyItems <- items
            historyIndex <- idx
            updateHistoryButtons()
            
            if not (isNull historyBack) then
                historyBack?addEventListener("click", fun (event: obj) ->
                    event?preventDefault()
                    if historyIndex > 0 then
                        historyIndex <- historyIndex - 1
                        persistHistoryIndex()
                        applyHistory()
                        updateHistoryButtons())
            
            if not (isNull historyForward) then
                historyForward?addEventListener("click", fun (event: obj) ->
                    event?preventDefault()
                    if historyIndex < historyItems.Length - 1 then
                        historyIndex <- historyIndex + 1
                        persistHistoryIndex()
                        applyHistory()
                        updateHistoryButtons())

    // ===== Keyboard Shortcuts & Status Line =====
    let initKeyboardShortcuts () =
        let textarea = document.getElementById "text"
        let form = if isNull textarea then null else textarea?closest("form")
        
        if not (isNull textarea) then
            textarea?addEventListener("keydown", fun (event: obj) ->
                let ctrlKey = unbox<bool> (event?ctrlKey)
                let metaKey = unbox<bool> (event?metaKey)
                let key = unbox<string> (event?key)
                
                if (ctrlKey || metaKey) && key = "Enter" then
                    event?preventDefault()
                    let sendBtn = document.getElementById "send-text"
                    if not (isNull sendBtn) then
                        let disabled = unbox<bool> (sendBtn?disabled)
                        if not disabled then
                            form?submit())

    let initFormSubmitHandler () =
        let textarea = document.getElementById "text"
        let form = if isNull textarea then null else textarea?closest("form")
        let statusLine = document.getElementById "status-line"
        
        if not (isNull form) then
            form?addEventListener("submit", fun (_: obj) ->
                // Update status line
                if not (isNull statusLine) then
                    statusLine?textContent <- "Sending..."
                    statusLine?classList.add("sending")
                    statusLine?classList.remove("sent")
                
                // Add history entry
                let privateSendCheckbox = document.getElementById "private-send"
                let isPrivateSend = if isNull privateSendCheckbox then false else unbox<bool> (privateSendCheckbox?``checked``)
                
                if not isPrivateSend then
                    let text = unbox<string> (textarea?value)
                    let items = addEntry text
                    historyItems <- items
                    historyIndex <- items.Length - 1
                    updateHistoryButtons()
                
                // Simulate sent state after short delay
                globalThis.setTimeout((fun () ->
                    if not (isNull statusLine) then
                        statusLine?textContent <- "Sent ✓"
                        statusLine?classList.remove("sending")
                        statusLine?classList.add("sent")
                        
                        globalThis.setTimeout((fun () ->
                            statusLine?textContent <- "Ready"
                            statusLine?classList.remove("sent")), 3000.0) |> ignore), 200.0) |> ignore)

    // ===== History Toggle =====
    let initHistoryToggle () =
        let historyToggle = document.getElementById "history-toggle"
        let historyList = document.getElementById "history-list"
        
        if not (isNull historyToggle) && not (isNull historyList) then
            historyToggle?addEventListener("click", fun (event: obj) ->
                event?preventDefault()
                historyList?classList.toggle("hidden"))

    // ===== Main Initialization =====
    let init () =
        initCopyButton()
        initStatusRefresh()
        initAutoRetry()
        setupWebSocketConnection()
        initTokenButtons()
        initHistoryNavigation()
        initKeyboardShortcuts()
        initFormSubmitHandler()
        initHistoryToggle()
        // Initial status load
        refreshStatus()

    let start () =
        if document.readyState = "loading" then
            document.addEventListener("DOMContentLoaded", fun _ -> init())
        else
            init()

let () = Sender.start()
