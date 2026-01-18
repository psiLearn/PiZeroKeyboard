namespace SenderApp

module UsbStatusPayload =
    open System.Text.Json
    open Giraffe
    open SenderApp.UsbStatusService

    type StatusPayload =
        { Text: string
          CssClass: string }

    let private statusJsonOptions =
        JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)

    let buildStatusPayload () =
        let status = readUsbStatus ()
        let payload =
            { Text = status.Text
              CssClass = status.CssClass }
        JsonSerializer.Serialize(payload, statusJsonOptions)

    let statusHandler : HttpHandler =
        fun next ctx ->
            task {
                let payload = buildStatusPayload ()
                ctx.SetHttpHeader("Content-Type", "application/json; charset=utf-8")
                return! text payload next ctx
            }
