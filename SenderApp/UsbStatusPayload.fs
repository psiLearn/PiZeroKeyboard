namespace SenderApp

module UsbStatusPayload =
    open System.Text.Json
    open Giraffe
    open Microsoft.AspNetCore.Http
    open SenderApp.CapsLockService
    open SenderApp.UsbStatusService

    type StatusPayload =
        { Text: string
          CssClass: string
          CapsText: string
          CapsCssClass: string }

    let private statusJsonOptions =
        JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)

    let buildStatusPayload () =
        let status = readUsbStatus ()
        let caps = readCapsLockStatus ()
        let payload =
            { Text = status.Text
              CssClass = status.CssClass
              CapsText = caps.Text
              CapsCssClass = caps.CssClass }
        JsonSerializer.Serialize(payload, statusJsonOptions)

    let statusHandler : HttpHandler =
        fun next ctx ->
            task {
                let payload = buildStatusPayload ()
                ctx.Response.ContentType <- "application/json; charset=utf-8"
                do! ctx.Response.WriteAsync(payload)
                return Some ctx
            }
