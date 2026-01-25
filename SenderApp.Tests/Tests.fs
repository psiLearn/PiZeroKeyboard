namespace SenderApp.Tests

open System
open System.IO
open System.Net
open System.Net.Http
open System.Net.Sockets
open System.Net.WebSockets
open System.Text
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging.Abstractions
open Giraffe
open SenderApp
open SenderApp.Cli
open SenderApp.ReceiverClient
open SenderApp.CapsLockService
open SenderApp.Configuration
open SenderApp.Handlers
open SenderApp.SendingControlsService
open SenderApp.ConnectionRetryService
open SenderApp.UsbStatusPayload
open SenderApp.UsbStatusService
open SenderApp.UsbStatusWatchers
open SenderApp.CapsLockModel
open SenderApp.UsbStatusModel
open SenderApp.Routes
open SenderApp.Views
open Xunit

[<assembly: CollectionBehavior(DisableTestParallelization = true)>]
do ()

module Tests =

    let withEnv name (value: string option) action =
        let prior = Environment.GetEnvironmentVariable name
        match value with
        | Some v -> Environment.SetEnvironmentVariable(name, v)
        | None -> Environment.SetEnvironmentVariable(name, null)
        try
            action ()
        finally
            Environment.SetEnvironmentVariable(name, prior)

    let withTempFile (content: string) action =
        let path = Path.GetTempFileName()
        File.WriteAllText(path, content)
        try
            action path
        finally
            File.Delete(path)

    let withEnvTask name (value: string option) action =
        task {
            let prior = Environment.GetEnvironmentVariable name
            match value with
            | Some v -> Environment.SetEnvironmentVariable(name, v)
            | None -> Environment.SetEnvironmentVariable(name, null)
            try
                return! action ()
            finally
                Environment.SetEnvironmentVariable(name, prior)
        }

    let withTempFileTask (content: string) action =
        task {
            let path = Path.GetTempFileName()
            File.WriteAllText(path, content)
            try
                return! action path
            finally
                File.Delete(path)
        }

    let createServer settings =
        let builder =
            WebHostBuilder()
                .ConfigureServices(fun services ->
                    services.AddGiraffe() |> ignore)
                .Configure(fun app ->
                    app.UseWebSockets() |> ignore
                    app.UseGiraffe(webApp settings))
        new TestServer(builder)

    let receiveWebSocketText (socket: WebSocket) (cancellationToken: CancellationToken) =
        task {
            let buffer = Array.zeroCreate<byte> 1024
            let segment = ArraySegment<byte>(buffer)
            let builder = StringBuilder()
            let mutable finished = false
            while not finished do
                let! result = socket.ReceiveAsync(segment, cancellationToken)
                if result.MessageType = WebSocketMessageType.Close then
                    finished <- true
                else
                    builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count)) |> ignore
                    finished <- result.EndOfMessage
            return builder.ToString()
        }

    [<Fact>]
    let ``statusFromState maps configured`` () =
        let result = statusFromState "configured"
        Assert.Equal("Raspberry Pi USB: connected (configured)", result.Text)
        Assert.Equal("connected", result.CssClass)

    [<Fact>]
    let ``statusFromState maps not attached`` () =
        let result = statusFromState "not attached"
        Assert.Equal("Raspberry Pi USB: not attached", result.Text)
        Assert.Equal("disconnected", result.CssClass)

    [<Fact>]
    let ``statusFromState maps pending states`` () =
        let attached = statusFromState "attached"
        let powered = statusFromState "powered"
        let def = statusFromState "default"

        Assert.Equal("Raspberry Pi USB: attached (not configured yet)", attached.Text)
        Assert.Equal("pending", attached.CssClass)
        Assert.Equal("Raspberry Pi USB: powered (not configured yet)", powered.Text)
        Assert.Equal("pending", powered.CssClass)
        Assert.Equal("Raspberry Pi USB: default (not configured yet)", def.Text)
        Assert.Equal("pending", def.CssClass)

    [<Fact>]
    let ``statusFromState maps empty and null`` () =
        let empty = statusFromState ""
        let whitespace = statusFromState "  "
        let nullState = statusFromState null

        Assert.Equal("Raspberry Pi USB: unknown (empty state)", empty.Text)
        Assert.Equal("unknown", empty.CssClass)
        Assert.Equal("Raspberry Pi USB: unknown (empty state)", whitespace.Text)
        Assert.Equal("unknown", whitespace.CssClass)
        Assert.Equal("Raspberry Pi USB: unknown (empty state)", nullState.Text)
        Assert.Equal("unknown", nullState.CssClass)

    [<Fact>]
    let ``statusFromState maps unknown`` () =
        let result = statusFromState "mystery"
        Assert.Equal("Raspberry Pi USB: mystery", result.Text)
        Assert.Equal("unknown", result.CssClass)

    [<Fact>]
    let ``statusMissingStateFile returns unknown`` () =
        let result = statusMissingStateFile ()
        Assert.Equal("Raspberry Pi USB: unknown (state file not found)", result.Text)
        Assert.Equal("unknown", result.CssClass)

    [<Fact>]
    let ``statusReadError returns unknown`` () =
        let result = statusReadError "boom"
        Assert.Equal("Raspberry Pi USB: unknown (boom)", result.Text)
        Assert.Equal("unknown", result.CssClass)

    [<Fact>]
    let ``caps lock maps on`` () =
        let result = statusFromValue "on"
        Assert.Equal("Caps Lock: on", result.Text)
        Assert.Equal("on", result.CssClass)

    [<Fact>]
    let ``caps lock maps off`` () =
        let result = statusFromValue "off"
        Assert.Equal("Caps Lock: off", result.Text)
        Assert.Equal("off", result.CssClass)

    [<Fact>]
    let ``caps lock maps unknown`` () =
        let empty = statusFromValue ""
        let other = statusFromValue "mystery"
        Assert.Equal("Caps Lock: unknown", empty.Text)
        Assert.Equal("unknown", empty.CssClass)
        Assert.Equal("Caps Lock: mystery", other.Text)
        Assert.Equal("unknown", other.CssClass)

    [<Fact>]
    let ``caps lock missing and error map to unknown`` () =
        let missing = statusMissingFile ()
        let error = CapsLockModel.statusReadError "boom"
        Assert.Equal("Caps Lock: unknown", missing.Text)
        Assert.Equal("unknown", missing.CssClass)
        Assert.Equal("Caps Lock: unknown", error.Text)
        Assert.Equal("unknown", error.CssClass)

    [<Fact>]
    let ``tryParsePort validates range`` () =
        Assert.Equal(Some 1, tryParsePort "1")
        Assert.Equal(Some 65535, tryParsePort "65535")
        Assert.Equal(None, tryParsePort "0")
        Assert.Equal(None, tryParsePort "65536")
        Assert.Equal(None, tryParsePort "nope")

    [<Fact>]
    let ``parseBool handles true and false values`` () =
        Assert.True(parseBool false "true")
        Assert.True(parseBool false "1")
        Assert.True(parseBool false "yes")
        Assert.True(parseBool false "on")
        Assert.False(parseBool true "false")
        Assert.False(parseBool true "0")
        Assert.False(parseBool true "no")
        Assert.False(parseBool true "off")

    [<Fact>]
    let ``parseBool returns default for unknown`` () =
        Assert.True(parseBool true "maybe")
        Assert.False(parseBool false "maybe")

    [<Fact>]
    let ``envOrDefault returns fallback and value`` () =
        let fallback =
            withEnv "SENDER_SAMPLE" None (fun () -> envOrDefault "SENDER_SAMPLE" "fallback")
        let value =
            withEnv "SENDER_SAMPLE" (Some "value") (fun () -> envOrDefault "SENDER_SAMPLE" "fallback")
        Assert.Equal("fallback", fallback)
        Assert.Equal("value", value)

    [<Fact>]
    let ``normalizeLayout maps aliases`` () =
        Assert.Equal("de", normalizeLayout "en" "de")
        Assert.Equal("de", normalizeLayout "en" "de-de")
        Assert.Equal("de", normalizeLayout "en" "german")
        Assert.Equal("en", normalizeLayout "de" "en")
        Assert.Equal("en", normalizeLayout "de" "en-us")
        Assert.Equal("en", normalizeLayout "de" "us")

    [<Fact>]
    let ``normalizeLayout falls back to default`` () =
        Assert.Equal("en", normalizeLayout "en" " ")
        Assert.Equal("de", normalizeLayout "de" "unknown")

    [<Fact>]
    let ``applyLayoutToken prefixes when missing`` () =
        let result = applyLayoutToken "de" "Hallo"
        Assert.Equal("{LAYOUT=de}Hallo", result)

    [<Fact>]
    let ``applyLayoutToken keeps existing token`` () =
        let result = applyLayoutToken "de" "  {LAYOUT=de}Hallo"
        Assert.Equal("  {LAYOUT=de}Hallo", result)

    [<Fact>]
    let ``applyLayoutToken ignores empty layout`` () =
        let result = applyLayoutToken "" "Hallo"
        Assert.Equal("Hallo", result)

    [<Fact>]
    let ``applyLayoutToken keeps existing token with colon`` () =
        let result = applyLayoutToken "de" "{LAYOUT:de}Hallo"
        Assert.Equal("{LAYOUT:de}Hallo", result)

    [<Fact>]
    let ``isDevelopment reads ASPNETCORE_ENVIRONMENT`` () =
        let dev = withEnv "ASPNETCORE_ENVIRONMENT" (Some "Development") (fun () -> isDevelopment ())
        let prod = withEnv "ASPNETCORE_ENVIRONMENT" (Some "Production") (fun () -> isDevelopment ())
        let unset = withEnv "ASPNETCORE_ENVIRONMENT" None (fun () -> isDevelopment ())
        Assert.True(dev)
        Assert.False(prod)
        Assert.False(unset)

    [<Fact>]
    let ``getDefaultLayout reads environment`` () =
        let de = withEnv "SENDER_LAYOUT" (Some "de-de") (fun () -> getDefaultLayout ())
        let fallback = withEnv "SENDER_LAYOUT" None (fun () -> getDefaultLayout ())
        Assert.Equal("de", de)
        Assert.Equal("en", fallback)

    [<Fact>]
    let ``shouldSendLayoutToken reads environment`` () =
        let enabled = withEnv "SENDER_LAYOUT_TOKEN" (Some "true") (fun () -> shouldSendLayoutToken ())
        let disabled = withEnv "SENDER_LAYOUT_TOKEN" (Some "no") (fun () -> shouldSendLayoutToken ())
        Assert.True(enabled)
        Assert.False(disabled)

    [<Fact>]
    let ``getCapsLockPath honors configured value`` () =
        let custom = withEnv "SENDER_CAPSLOCK_PATH" (Some "C:\\temp\\capslock") (fun () -> getCapsLockPath ())
        let none = withEnv "SENDER_CAPSLOCK_PATH" (Some " ") (fun () -> getCapsLockPath ())
        Assert.Equal(Some "C:\\temp\\capslock", custom)
        Assert.Equal(None, none)

    [<Fact>]
    let ``readCapsLockStatus reads file`` () =
        let result =
            withTempFile "on" (fun path ->
                withEnv "SENDER_CAPSLOCK_PATH" (Some path) (fun () -> readCapsLockStatus ()))
        Assert.Equal("Caps Lock: on", result.Text)
        Assert.Equal("on", result.CssClass)

    [<Fact>]
    let ``readCapsLockStatus handles missing file`` () =
        let missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
        let result = withEnv "SENDER_CAPSLOCK_PATH" (Some missingPath) (fun () -> readCapsLockStatus ())
        Assert.Equal("Caps Lock: unknown", result.Text)
        Assert.Equal("unknown", result.CssClass)

    [<Fact>]
    let ``readCapsLockStatus handles read errors`` () =
        let path = Path.GetTempFileName()
        File.WriteAllText(path, "on")
        let result =
            use locked =
                new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
            withEnv "SENDER_CAPSLOCK_PATH" (Some path) (fun () -> readCapsLockStatus ())
        Assert.Equal("Caps Lock: unknown", result.Text)
        Assert.Equal("unknown", result.CssClass)
        File.Delete(path)

    [<Fact>]
    let ``getUsbEventPath honors environment`` () =
        let none = withEnv "SENDER_USB_EVENT_PATH" None (fun () -> getUsbEventPath ())
        let value = withEnv "SENDER_USB_EVENT_PATH" (Some "/tmp/usb-events") (fun () -> getUsbEventPath ())
        Assert.Equal(None, none)
        Assert.Equal(Some "/tmp/usb-events", value)

    [<Fact>]
    let ``tryGetUsbStatePath uses configured path`` () =
        let result =
            withTempFile "configured" (fun path ->
                withEnv "SENDER_USB_STATE_PATH" (Some path) (fun () -> tryGetUsbStatePath ()))
        Assert.True(result.IsSome)

    [<Fact>]
    let ``tryGetUsbStatePath returns none when missing`` () =
        let missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
        let result = withEnv "SENDER_USB_STATE_PATH" (Some missingPath) (fun () -> tryGetUsbStatePath ())
        Assert.Equal(None, result)

    [<Fact>]
    let ``readUsbStatus reads configured`` () =
        let result =
            withTempFile "configured" (fun path ->
                withEnv "SENDER_USB_STATE_PATH" (Some path) (fun () -> readUsbStatus ()))
        Assert.Equal("Raspberry Pi USB: connected (configured)", result.Text)
        Assert.Equal("connected", result.CssClass)

    [<Fact>]
    let ``readUsbStatus handles read errors`` () =
        let path = Path.GetTempFileName()
        File.WriteAllText(path, "configured")
        let result =
            use locked =
                new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
            withEnv "SENDER_USB_STATE_PATH" (Some path) (fun () -> readUsbStatus ())
        Assert.Equal("unknown", result.CssClass)
        Assert.StartsWith("Raspberry Pi USB: unknown", result.Text)
        File.Delete(path)

    [<Fact>]
    let ``buildStatusPayload includes caps and usb values`` () =
        let payloadJson =
            withTempFile "configured" (fun usbPath ->
                withTempFile "on" (fun capsPath ->
                    withEnv "SENDER_USB_STATE_PATH" (Some usbPath) (fun () ->
                        withEnv "SENDER_CAPSLOCK_PATH" (Some capsPath) (fun () ->
                            buildStatusPayload ()))))

        use doc = JsonDocument.Parse(payloadJson)
        let root = doc.RootElement
        Assert.Equal("Raspberry Pi USB: connected (configured)", root.GetProperty("text").GetString())
        Assert.Equal("connected", root.GetProperty("cssClass").GetString())
        Assert.Equal("Caps Lock: on", root.GetProperty("capsText").GetString())
        Assert.Equal("on", root.GetProperty("capsCssClass").GetString())

    [<Fact>]
    let ``statusHandler returns json with headers`` () =
        let (ctx, json) =
            withTempFile "configured" (fun usbPath ->
                withTempFile "off" (fun capsPath ->
                    withEnv "SENDER_USB_STATE_PATH" (Some usbPath) (fun () ->
                        withEnv "SENDER_CAPSLOCK_PATH" (Some capsPath) (fun () ->
                            let ctx = DefaultHttpContext()
                            use body = new MemoryStream()
                            ctx.Response.Body <- body
                            let next: HttpFunc = fun _ -> task { return None }
                            statusHandler next ctx
                            |> Async.AwaitTask
                            |> Async.RunSynchronously
                            |> ignore
                            body.Position <- 0L
                            use reader = new StreamReader(body)
                            (ctx, reader.ReadToEnd())))))

        Assert.Equal("application/json; charset=utf-8", ctx.Response.Headers["Content-Type"].ToString())
        use doc = JsonDocument.Parse(json)
        let root = doc.RootElement
        Assert.Equal("Raspberry Pi USB: connected (configured)", root.GetProperty("text").GetString())
        Assert.Equal("Caps Lock: off", root.GetProperty("capsText").GetString())

    [<Fact>]
    let ``usb status watchers refresh and dispose`` () =
        let dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(dir) |> ignore
        let statePath = Path.Combine(dir, "state")
        let eventPath = Path.Combine(dir, "events")
        let capsPath = Path.Combine(dir, "caps")
        File.WriteAllText(statePath, "configured")
        let watchers = create (fun () -> Some statePath) (Some eventPath) (Some capsPath)
        try
            watchers.RefreshStateWatcher()
        finally
            watchers.Dispose()
            Directory.Delete(dir, true)

    [<Fact>]
    let ``isMobileClient detects mobile user agents`` () =
        let mobileCtx = DefaultHttpContext()
        mobileCtx.Request.Headers["User-Agent"] <- "Mozilla/5.0 (iPhone; CPU iPhone OS 15_0 like Mac OS X)"
        Assert.True(isMobileClient mobileCtx)

        let desktopCtx = DefaultHttpContext()
        desktopCtx.Request.Headers["User-Agent"] <- "Mozilla/5.0 (X11; Linux x86_64)"
        Assert.False(isMobileClient desktopCtx)

        let emptyCtx = DefaultHttpContext()
        Assert.False(isMobileClient emptyCtx)

    [<Fact>]
    let ``view helpers return expected attribute counts`` () =
        Assert.Empty(bodyAttrs false)
        Assert.Equal(1, (bodyAttrs true).Length)

        let selected = layoutOptionAttrs "en" "en"
        let unselected = layoutOptionAttrs "en" "de"
        Assert.Equal(2, selected.Length)
        Assert.Equal(1, unselected.Length)

    [<Fact>]
    let ``buildStatusNodes respects flags`` () =
        let settings: SenderSettings = { TargetIp = "127.0.0.1"; TargetPort = 5000 }
        let nodes1 = buildStatusNodes true settings Idle None
        let nodes2 = buildStatusNodes false settings (Success 1) None
        let nodes3 = buildStatusNodes true settings (Success 1) None
        let nodes4 = buildStatusNodes true settings (Failure "oops") None
        let nodes5 = buildStatusNodes true settings (Sending { BytesSent = 50; TotalBytes = 100 }) None
        
        Assert.Empty(nodes1)
        Assert.Equal(1, nodes2.Length)
        Assert.Equal(1, nodes3.Length)
        Assert.Equal(1, nodes4.Length)
        Assert.Equal(1, nodes5.Length)

    [<Fact>]
    let ``renderHeader includes target only when enabled`` () =
        let settings: SenderSettings = { TargetIp = "127.0.0.1"; TargetPort = 5000 }
        let model: IndexViewModel =
            { Status = Idle
              Text = ""
              UsbStatus = statusFromState "configured"
              CapsLock = statusFromValue "off"
              IsMobile = false
              Layout = "en"
              ConnectionStatus = NotConnected { Reason = "Test"; LastAttempt = None; RetryCount = 0; Suggestion = "Test suggestion" }
              KeyboardVisibility = Visible
              SendingControls = SendingControlsService.defaultControls
              RetryState = ConnectionRetryService.defaultRetryState
              AutoRetryEnabled = false
              SendStartTime = None }
        let withTarget = renderHeader settings model true
        let withoutTarget = renderHeader settings model false
        Assert.Equal(3, withTarget.Length)
        Assert.Equal(2, withoutTarget.Length)

    [<Fact>]
    let ``status websocket sends initial payload`` () =
        task {
            let settings: SenderSettings = { TargetIp = "127.0.0.1"; TargetPort = 5000 }
            let! (payload: string) =
                withTempFileTask "configured" (fun usbPath ->
                    withTempFileTask "on" (fun capsPath ->
                        withEnvTask "SENDER_USB_STATE_PATH" (Some usbPath) (fun () ->
                            withEnvTask "SENDER_CAPSLOCK_PATH" (Some capsPath) (fun () ->
                                task {
                                    use server = createServer settings
                                    let client: WebSocketClient = server.CreateWebSocketClient()
                                    let uri = Uri("ws://localhost/status/ws")
                                    use cts = new CancellationTokenSource(5000)
                                    let! (socket: WebSocket) = client.ConnectAsync(uri, cts.Token)
                                    use socket = socket
                                    let! message = receiveWebSocketText socket cts.Token
                                    do!
                                        socket.CloseAsync(
                                            WebSocketCloseStatus.NormalClosure,
                                            "done",
                                            CancellationToken.None
                                        )
                                    return message
                                }))))

            use doc = JsonDocument.Parse(payload)
            let root = doc.RootElement
            Assert.Equal("Raspberry Pi USB: connected (configured)", root.GetProperty("text").GetString())
            Assert.Equal("Caps Lock: on", root.GetProperty("capsText").GetString())
        }

    [<Fact>]
    let ``status websocket rejects non websocket requests`` () =
        task {
            let settings: SenderSettings = { TargetIp = "127.0.0.1"; TargetPort = 5000 }
            use server = createServer settings
            use client = server.CreateClient()
            let! response = client.GetAsync("/status/ws")
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode)
            let! body = response.Content.ReadAsStringAsync()
            Assert.Contains("WebSocket endpoint", body)
        }

    [<Fact>]
    let ``healthz returns ok`` () =
        task {
            let settings: SenderSettings = { TargetIp = "127.0.0.1"; TargetPort = 5000 }
            use server = createServer settings
            use client = server.CreateClient()
            let! response = client.GetAsync("/healthz")
            Assert.Equal(HttpStatusCode.OK, response.StatusCode)
            let! body = response.Content.ReadAsStringAsync()
            Assert.Equal("OK", body)
        }

    [<Fact>]
    let ``index page includes history controls and scripts`` () =
        task {
            let settings: SenderSettings = { TargetIp = "127.0.0.1"; TargetPort = 5000 }
            use server = createServer settings
            use client = server.CreateClient()
            let! response = client.GetAsync("/")
            Assert.Equal(HttpStatusCode.OK, response.StatusCode)
            let! body = response.Content.ReadAsStringAsync()
            Assert.Contains("id=\"history-prev\"", body)
            Assert.Contains("id=\"history-next\"", body)
            Assert.Contains("src=\"/history.js\"", body)
            Assert.Contains("src=\"/sender.js\"", body)
        }

    [<Fact>]
    let ``sendOnceCli rejects invalid port`` () =
        let result = sendOnceCli "127.0.0.1" "nope" "hello"
        Assert.Equal(1, result)

    [<Fact>]
    let ``cli run returns usage on help`` () =
        let result = run [| "--help" |]
        Assert.Equal(1, result)

    [<Fact>]
    let ``cli run rejects invalid port argument`` () =
        let result = run [| "127.0.0.1"; "abc"; "hello" |]
        Assert.Equal(1, result)

    [<Fact>]
    let ``sendOnce writes payload to receiver`` () =
        task {
            use listener = new TcpListener(IPAddress.Loopback, 0)
            listener.Start()
            let port = (listener.LocalEndpoint :?> IPEndPoint).Port
            let acceptTask = listener.AcceptTcpClientAsync()

            let exitCode = sendOnce "127.0.0.1" port "hello"
            Assert.Equal(0, exitCode)

            use client = acceptTask.Result
            use stream = client.GetStream()
            let buffer = Array.zeroCreate<byte> 32
            let! read = stream.ReadAsync(buffer, 0, buffer.Length)
            let received = Encoding.UTF8.GetString(buffer, 0, read)
            Assert.Equal("hello", received)
        }

    [<Fact>]
    let ``sendOnce returns failure on closed port`` () =
        use listener = new TcpListener(IPAddress.Loopback, 0)
        listener.Start()
        let port = (listener.LocalEndpoint :?> IPEndPoint).Port
        listener.Stop()
        let exitCode = sendOnce "127.0.0.1" port "hello"
        Assert.Equal(1, exitCode)

    [<Fact>]
    let ``sendToReceiver returns bytes on success`` () =
        task {
            use listener = new TcpListener(IPAddress.Loopback, 0)
            listener.Start()
            let port = (listener.LocalEndpoint :?> IPEndPoint).Port
            let acceptTask = listener.AcceptTcpClientAsync()
            let logger = NullLogger.Instance
            let! result =
                sendToReceiver logger { TargetIp = "127.0.0.1"; TargetPort = port } "ping"

            match result with
            | Ok count -> Assert.Equal(4, count)
            | Error msg -> Assert.True(false, msg)

            use client = acceptTask.Result
            use stream = client.GetStream()
            let buffer = Array.zeroCreate<byte> 8
            let! read = stream.ReadAsync(buffer, 0, buffer.Length)
            let received = Encoding.UTF8.GetString(buffer, 0, read)
            Assert.Equal("ping", received)
        }

    [<Fact>]
    let ``sendToReceiver returns error on failure`` () =
        task {
            use listener = new TcpListener(IPAddress.Loopback, 0)
            listener.Start()
            let port = (listener.LocalEndpoint :?> IPEndPoint).Port
            listener.Stop()
            let logger = NullLogger.Instance
            let! result =
                sendToReceiver logger { TargetIp = "127.0.0.1"; TargetPort = port } "ping"
            match result with
            | Ok _ -> Assert.True(false, "Expected failure.")
            | Error _ -> Assert.True(true)
        }
