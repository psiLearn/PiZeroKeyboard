namespace SenderApp

module Handlers =
    open System
    open Microsoft.AspNetCore.Http
    open Giraffe
    open SenderApp.Configuration
    open SenderApp.ReceiverClient
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

    let indexHandler settings : HttpHandler =
        fun next ctx ->
            let showDevInfo = isDevelopment ()
            let layout = getDefaultLayout ()
            let model =
                { Status = Idle
                  Text = ""
                  UsbStatus = readUsbStatus ()
                  IsMobile = isMobileClient ctx
                  Layout = layout }
            renderPage settings model showDevInfo next ctx

    let sendHandler (settings: SenderSettings) : HttpHandler =
        fun next ctx ->
            task {
                let isMobile = isMobileClient ctx
                let showDevInfo = isDevelopment ()
                let defaultLayout = getDefaultLayout ()
                let sendLayoutToken = shouldSendLayoutToken ()
                let! form = ctx.BindFormAsync<SendRequest>()

                let text =
                    form.Text
                    |> Option.ofObj
                    |> Option.defaultValue ""
                    |> fun t -> t.TrimEnd()

                let layout = normalizeLayout defaultLayout form.Layout

                if String.IsNullOrWhiteSpace text then
                    let model =
                        { Status = Failure "Please enter some text before sending."
                          Text = ""
                          UsbStatus = readUsbStatus ()
                          IsMobile = isMobile
                          Layout = layout }

                    return! (renderPage settings model showDevInfo) next ctx
                else
                    let logger = ctx.GetLogger()
                    let payload =
                        if sendLayoutToken then
                            applyLayoutToken layout text
                        else
                            text
                    let! result = sendToReceiver logger settings payload

                    match result with
                    | Ok bytes ->
                        let model =
                            { Status = Success bytes
                              Text = ""
                              UsbStatus = readUsbStatus ()
                              IsMobile = isMobile
                              Layout = layout }

                        return! (renderPage settings model showDevInfo) next ctx
                    | Error message ->
                        let model =
                            { Status = Failure message
                              Text = text
                              UsbStatus = readUsbStatus ()
                              IsMobile = isMobile
                              Layout = layout }

                        return! (renderPage settings model showDevInfo) next ctx
            }
