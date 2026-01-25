namespace SenderApp

module ConnectionService =
    open System
    open System.Net.Sockets
    open Microsoft.Extensions.Logging

    let private connectionTimeoutSeconds = 2.0

    /// Safely extract error message, preventing information disclosure
    let private sanitizeErrorMessage (ex: Exception) : string =
        match ex with
        | :? SocketException -> "Network error"
        | :? TimeoutException -> "Connection timeout"
        | :? OperationCanceledException -> "Connection cancelled"
        | _ -> "Connection failed"

    /// Provide actionable suggestion based on error type
    let private getSuggestion (reason: string) : string =
        match reason with
        | "Connection timeout" -> 
            "Check: (1) Receiver service is running. (2) Host/port is reachable. (3) Firewall allows connection."
        | "Network error" -> 
            "Network unreachable. Check target host is online and network is connected."
        | "Connection cancelled" ->
            "Connection was cancelled. Try reconnecting."
        | _ -> 
            "Try reconnecting or check target configuration."

    /// Check if the receiver is reachable and measure latency
    let checkConnection (logger: ILogger) (settings: SenderSettings) : ConnectionStatus =
        try
            let stopwatch = System.Diagnostics.Stopwatch.StartNew()
            use client = new TcpClient()
            let connectTask = client.ConnectAsync(settings.TargetIp, settings.TargetPort)
            let completed = connectTask.Wait(TimeSpan.FromSeconds(connectionTimeoutSeconds))

            if completed && client.Connected then
                stopwatch.Stop()
                let latency = int stopwatch.ElapsedMilliseconds
                logger.LogInformation(
                    "Connected to receiver at {Ip}:{Port} with latency {LatencyMs}ms",
                    settings.TargetIp,
                    settings.TargetPort,
                    latency
                )
                Connected
                    { LastActivity = DateTime.UtcNow
                      LatencyMs = Some latency }
            else
                let reason = "Connection timeout"
                logger.LogWarning(
                    "Connection timeout to {Ip}:{Port}",
                    settings.TargetIp,
                    settings.TargetPort
                )
                NotConnected 
                    { Reason = reason
                      LastAttempt = Some DateTime.UtcNow
                      RetryCount = 0
                      Suggestion = getSuggestion reason }
        with ex ->
            let reason = sanitizeErrorMessage ex
            logger.LogWarning(
                ex,
                "Failed to connect to {Ip}:{Port}: {Reason}",
                settings.TargetIp,
                settings.TargetPort,
                reason
            )
            NotConnected 
                { Reason = reason
                  LastAttempt = Some DateTime.UtcNow
                  RetryCount = 0
                  Suggestion = getSuggestion reason }

    /// Format connection status for display
    let formatConnectionStatus (settings: SenderSettings) (status: ConnectionStatus) : string =
        match status with
        | Connected info ->
            match info.LatencyMs with
            | Some latency ->
                sprintf "✓ Connected to %s:%d (%dms)" settings.TargetIp settings.TargetPort latency
            | None ->
                sprintf "✓ Connected to %s:%d" settings.TargetIp settings.TargetPort
        | NotConnected info ->
            sprintf "✕ Not connected: %s" info.Reason

    /// Get CSS class for connection status badge
    let getConnectionCssClass (status: ConnectionStatus) : string =
        match status with
        | Connected _ -> "connection-connected"
        | NotConnected _ -> "connection-disconnected"

    /// Format last attempt time for display
    let formatLastAttempt (lastAttempt: DateTime option) : string =
        match lastAttempt with
        | Some dt -> sprintf "Last attempt: %s" (dt.ToString("HH:mm:ss"))
        | None -> ""
