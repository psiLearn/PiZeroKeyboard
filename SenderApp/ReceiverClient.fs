namespace SenderApp

module ReceiverClient =
    open System.Net.Sockets
    open System.Text
    open Microsoft.Extensions.Logging

    let sendToReceiver (logger: ILogger) (settings: SenderSettings) (text: string) =
        task {
            try
                use client = new TcpClient()
                do! client.ConnectAsync(settings.TargetIp, settings.TargetPort)
                use stream = client.GetStream()
                let payload = Encoding.UTF8.GetBytes text
                do! stream.WriteAsync(payload, 0, payload.Length)
                logger.LogInformation(
                    "Sent {ByteCount} bytes to {Ip}:{Port}",
                    payload.Length,
                    settings.TargetIp,
                    settings.TargetPort
                )
                return Ok payload.Length
            with ex ->
                logger.LogError(
                    ex,
                    "Failed to send payload to {Ip}:{Port}",
                    settings.TargetIp,
                    settings.TargetPort
                )
                return Error ex.Message
        }

    let sendOnce (ip: string) (port: int) (text: string) =
        try
            use client = new TcpClient()
            client.Connect(ip, port)
            use stream = client.GetStream()
            let payload = Encoding.UTF8.GetBytes text
            stream.Write(payload, 0, payload.Length)
            printfn "Sent %d bytes to %s:%d" payload.Length ip port
            0
        with ex ->
            eprintfn "Failed: %s" ex.Message
            1
