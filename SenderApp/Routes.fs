namespace SenderApp

module Routes =
    open Giraffe
    open SenderApp.Handlers
    open SenderApp.UsbStatusPayload
    open SenderApp.UsbStatusWebSocket

    let webApp settings =
        choose [
            GET >=> route "/" >=> indexHandler settings
            POST >=> route "/send" >=> sendHandler settings
            GET >=> route "/status" >=> statusHandler
            GET >=> route "/status/ws" >=> statusWebSocketHandler
            GET >=> route "/healthz" >=> text "OK"
        ]
