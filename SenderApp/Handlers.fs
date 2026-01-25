namespace SenderApp

module Handlers =
    open System
    open Microsoft.AspNetCore.Http
    open Giraffe
    open SenderApp.Configuration
    open SenderApp.CapsLockService
    open SenderApp.ConnectionService
    open SenderApp.ConnectionRetryService
    open SenderApp.ModifierKeyService
    open SenderApp.ReceiverClient
    open SenderApp.SendingControlsService
    open SenderApp.UsbStatusService
    open SenderApp.Views

    let isMobileClient (ctx: HttpContext) =
        match ctx.Request.Headers.TryGetValue "User-Agent" with
        | true, values ->
            let ua = values.ToString().ToLowerInvariant()
            ua.Contains("mobi")
            || ua.Contains("android")
            || ua.Contains("iphone")
            || ua.Contains("ipad")
        | _ -> false

    let buildModel isMobile layout status : IndexViewModel =
        { Status = status
          Text = ""
          UsbStatus = readUsbStatus ()
          CapsLock = readCapsLockStatus ()
          IsMobile = isMobile
          Layout = layout
          ConnectionStatus = NotConnected { Reason = "Not checked yet"; LastAttempt = None; RetryCount = 0; Suggestion = "Click Reconnect to check connection." }
          KeyboardVisibility = Visible
          SendingControls = SendingControlsService.defaultControls
          RetryState = defaultRetryState
          AutoRetryEnabled = false
          SendStartTime = None }

    let buildModelWithConnection (ctx: HttpContext) settings isMobile layout status : IndexViewModel =
        let logger = ctx.GetLogger()
        { Status = status
          Text = ""
          UsbStatus = readUsbStatus ()
          CapsLock = readCapsLockStatus ()
          IsMobile = isMobile
          Layout = layout
          ConnectionStatus = checkConnection logger settings
          KeyboardVisibility = Visible
          SendingControls = SendingControlsService.defaultControls
          RetryState = defaultRetryState
          AutoRetryEnabled = false
          SendStartTime = None }

    let indexHandler settings : HttpHandler =
        fun next ctx ->
            let showDevInfo = isDevelopment ()
            let layout = getDefaultLayout ()
            let model = buildModelWithConnection ctx settings (isMobileClient ctx) layout Idle
            renderPage settings model showDevInfo next ctx

    let private preparePayload sendLayoutToken layout text =
        if sendLayoutToken then applyLayoutToken layout text else text

    let private handleEmptyText ctx settings isMobile layout showDevInfo next =
        let model = buildModelWithConnection ctx settings isMobile layout (Failure "Please enter some text before sending.")
        renderPage settings model showDevInfo next ctx

    let private handleSuccessfulSend ctx settings isMobile layout showDevInfo bytes sendStartTime next =
        let model = 
            { buildModelWithConnection ctx settings isMobile layout (Success bytes) with
                Text = ""
                SendStartTime = Some sendStartTime }
        renderPage settings model showDevInfo next ctx

    let private handleFailedSend ctx settings isMobile layout showDevInfo message text next =
        let model = 
            { buildModelWithConnection ctx settings isMobile layout (Failure message) with
                Text = text }
        renderPage settings model showDevInfo next ctx

    let sendHandler (settings: SenderSettings) : HttpHandler =
        fun next ctx ->
            task {
                let isMobile = isMobileClient ctx
                let showDevInfo = isDevelopment ()
                let defaultLayout = getDefaultLayout ()
                let sendLayoutToken = shouldSendLayoutToken ()
                let! form = ctx.BindFormAsync<SendRequest>()

                let text = form.Text |> Option.ofObj |> Option.defaultValue ""
                let textForCheck = text.Trim()
                let layout = normalizeLayout defaultLayout form.Layout

                if String.IsNullOrWhiteSpace textForCheck then
                    return! handleEmptyText ctx settings isMobile layout showDevInfo next
                else
                    let logger = ctx.GetLogger()
                    let sendStartTime = DateTime.UtcNow
                    let payload = preparePayload sendLayoutToken layout text
                    let! result = sendToReceiver logger settings payload

                    match result with
                    | Ok bytes -> return! handleSuccessfulSend ctx settings isMobile layout showDevInfo bytes sendStartTime next
                    | Error message -> return! handleFailedSend ctx settings isMobile layout showDevInfo message text next
            }
