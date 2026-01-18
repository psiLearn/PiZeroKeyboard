namespace SenderApp

module UsbStatusWebSocket =
    open System
    open System.Net.WebSockets
    open System.Text
    open System.Threading
    open System.Threading.Tasks
    open Giraffe
    open Microsoft.AspNetCore.Http
    open SenderApp.UsbStatusPayload
    open SenderApp.UsbStatusService
    open SenderApp.UsbStatusWatchers

    let private sendWebSocketText (socket: WebSocket) (payload: string) (cancellationToken: CancellationToken) =
        task {
            let bytes = Encoding.UTF8.GetBytes payload
            let segment = ArraySegment<byte>(bytes)
            do! socket.SendAsync(segment, WebSocketMessageType.Text, true, cancellationToken)
        }

    let statusWebSocketHandler : HttpHandler =
        fun next ctx ->
            task {
                if not ctx.WebSockets.IsWebSocketRequest then
                    return! RequestErrors.badRequest (text "WebSocket endpoint.") next ctx
                else
                    let! socket = ctx.WebSockets.AcceptWebSocketAsync()
                    use socket = socket
                    use cancellationSource =
                        CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted)
                    let cancellationToken = cancellationSource.Token

                    let sendLoop =
                        task {
                            let mutable lastPayload = ""
                            let initialPayload = buildStatusPayload ()
                            do! sendWebSocketText socket initialPayload cancellationToken
                            lastPayload <- initialPayload

                            let watchers = UsbStatusWatchers.create tryGetUsbStatePath (getUsbEventPath ())
                            try
                                watchers.RefreshStateWatcher ()

                                while socket.State = WebSocketState.Open && not cancellationToken.IsCancellationRequested do
                                    let! _ = watchers.Channel.Reader.ReadAsync(cancellationToken)
                                    watchers.RefreshStateWatcher ()
                                    let payload = buildStatusPayload ()
                                    if payload <> lastPayload then
                                        do! sendWebSocketText socket payload cancellationToken
                                        lastPayload <- payload
                            finally
                                watchers.Dispose ()
                        }

                    let receiveLoop =
                        task {
                            let buffer = Array.zeroCreate<byte> 256
                            while socket.State = WebSocketState.Open && not cancellationToken.IsCancellationRequested do
                                let! result =
                                    socket.ReceiveAsync(ArraySegment<byte>(buffer), cancellationToken)
                                if result.MessageType = WebSocketMessageType.Close then
                                    do!
                                        socket.CloseAsync(
                                            WebSocketCloseStatus.NormalClosure,
                                            "Closing",
                                            CancellationToken.None
                                        )
                                    if not cancellationSource.IsCancellationRequested then
                                        cancellationSource.Cancel()
                        }

                    // Best-effort status push: ignore expected disconnect exceptions to keep logs quiet.
                    let safeTask (taskToWrap: Task) =
                        task {
                            try
                                do! taskToWrap
                            with
                            | :? OperationCanceledException -> ()
                            | :? WebSocketException -> ()
                            | :? ObjectDisposedException -> ()
                        }

                    let sendTask = safeTask sendLoop
                    let receiveTask = safeTask receiveLoop

                    let! completed = Task.WhenAny([| sendTask :> Task; receiveTask :> Task |])
                    cancellationSource.Cancel()
                    do! completed
                    do! Task.WhenAll([| sendTask :> Task; receiveTask :> Task |])
                    return Some ctx
            }
