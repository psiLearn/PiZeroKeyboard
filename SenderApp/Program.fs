open System
open System.IO
open System.Net.Sockets
open System.Text
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Giraffe
open Giraffe.ViewEngine

type SendStatus =
    | Idle
    | Success of int
    | Failure of string

type UsbStatus =
    { Text: string
      CssClass: string }

type IndexViewModel =
    { Status: SendStatus
      Text: string
      UsbStatus: UsbStatus
      IsMobile: bool
      Layout: string }

[<CLIMutable>]
type SendRequest =
    { Text: string
      Layout: string }

type SenderSettings =
    { TargetIp: string
      TargetPort: int }

let tryParsePort (value: string) =
    match Int32.TryParse value with
    | true, port when port > 0 && port <= 65535 -> Some port
    | _ -> None

let envOrDefault name fallback =
    match Environment.GetEnvironmentVariable name with
    | null | "" -> fallback
    | value -> value

let parseBool defaultValue (value: string) =
    match value.Trim().ToLowerInvariant() with
    | "1"
    | "true"
    | "yes"
    | "on" -> true
    | "0"
    | "false"
    | "no"
    | "off" -> false
    | _ -> defaultValue

let isDevelopment () =
    match envOrDefault "ASPNETCORE_ENVIRONMENT" "" with
    | value when value.Equals("Development", StringComparison.OrdinalIgnoreCase) -> true
    | _ -> false

let normalizeLayout defaultLayout (value: string) =
    if String.IsNullOrWhiteSpace value then
        defaultLayout
    else
        match value.Trim().ToLowerInvariant() with
        | "de"
        | "de-de"
        | "german" -> "de"
        | "en"
        | "en-us"
        | "us" -> "en"
        | _ -> defaultLayout

let getDefaultLayout () =
    envOrDefault "SENDER_LAYOUT" "en"
    |> normalizeLayout "en"

let shouldSendLayoutToken () =
    envOrDefault "SENDER_LAYOUT_TOKEN" "false"
    |> parseBool false

let applyLayoutToken (layout: string) (text: string) =
    if String.IsNullOrWhiteSpace layout then
        text
    else
        let trimmed = text.TrimStart()
        if trimmed.StartsWith("{LAYOUT=", StringComparison.OrdinalIgnoreCase)
           || trimmed.StartsWith("{LAYOUT:", StringComparison.OrdinalIgnoreCase) then
            text
        else
            sprintf "{LAYOUT=%s}%s" layout text

let tryGetUsbStatePath () =
    let configuredPath = envOrDefault "SENDER_USB_STATE_PATH" ""
    if not (String.IsNullOrWhiteSpace configuredPath) && File.Exists configuredPath then
        Some configuredPath
    elif Directory.Exists "/sys/class/udc" then
        let entries = Directory.GetDirectories "/sys/class/udc"
        if entries.Length > 0 then
            Some(Path.Combine(entries.[0], "state"))
        else
            None
    else
        None

let readUsbStatus () =
    match tryGetUsbStatePath () with
    | None ->
        { Text = "Raspberry Pi USB: unknown (state file not found)"
          CssClass = "unknown" }
    | Some path ->
        try
            let state = File.ReadAllText(path).Trim().ToLowerInvariant()
            match state with
            | "configured" ->
                { Text = "Raspberry Pi USB: connected (configured)"
                  CssClass = "connected" }
            | "not attached" ->
                { Text = "Raspberry Pi USB: not attached"
                  CssClass = "disconnected" }
            | "attached"
            | "powered"
            | "default" ->
                { Text = sprintf "Raspberry Pi USB: %s (not configured yet)" state
                  CssClass = "pending" }
            | "" ->
                { Text = "Raspberry Pi USB: unknown (empty state)"
                  CssClass = "unknown" }
            | other ->
                { Text = sprintf "Raspberry Pi USB: %s" other
                  CssClass = "unknown" }
        with ex ->
            { Text = sprintf "Raspberry Pi USB: unknown (%s)" ex.Message
              CssClass = "unknown" }

let jsonEscape (value: string) =
    if isNull value then
        ""
    else
        value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t")

let statusHandler : HttpHandler =
    fun next ctx ->
        task {
            let status = readUsbStatus ()
            let payload =
                sprintf
                    "{\"text\":\"%s\",\"cssClass\":\"%s\"}"
                    (jsonEscape status.Text)
                    (jsonEscape status.CssClass)
            ctx.SetHttpHeader("Content-Type", "application/json; charset=utf-8")
            return! text payload next ctx
        }

let isMobileClient (ctx: HttpContext) =
    match ctx.Request.Headers.TryGetValue "User-Agent" with
    | true, values ->
        let ua = values.ToString().ToLowerInvariant()
        ua.Contains("mobi")
        || ua.Contains("android")
        || ua.Contains("iphone")
        || ua.Contains("ipad")
    | _ -> false

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

let renderPage (settings: SenderSettings) (model: IndexViewModel) : HttpHandler =
    let showSuccess = isDevelopment ()
    let statusNodes =
        match model.Status with
        | Idle -> []
        | Success bytes ->
            if showSuccess then
                [ div
                      [ _class "alert success" ]
                      [ str (sprintf "Sent %d bytes to %s:%d." bytes settings.TargetIp settings.TargetPort) ] ]
            else
                []
        | Failure message ->
            [ div
                  [ _class "alert error" ]
                  [ str (sprintf "Failed to send text: %s" message) ] ]

    let bodyAttrs =
        if model.IsMobile then
            [ _class "mobile" ]
        else
            []

    let layoutOptionAttrs value =
        if String.Equals(model.Layout, value, StringComparison.OrdinalIgnoreCase) then
            [ _value value; attr "selected" "selected" ]
        else
            [ _value value ]

    let specialKeyButton label token =
        button
            [ _type "button"
              _class "secondary"
              attr "data-token" token ]
            [ str label ]

    let formNodes =
        [ div
              [ _class "layout-row" ]
              [ label [ _for "layout" ] [ str "Keyboard layout" ]
                select [ _id "layout"; _name "layout" ] [
                    option (layoutOptionAttrs "en") [ str "English (US)" ]
                    option (layoutOptionAttrs "de") [ str "Deutsch (DE)" ]
                ] ]
          textarea
              [ _id "text"; _name "text"; _placeholder "Paste text here..." ]
              [ str model.Text ]
          button [ _type "submit"; _class "primary send" ] [ str "Send" ]
          div [ _class "special-keys" ] [
              div [ _class "key-row" ] [
                  specialKeyButton "Esc" "{ESC}"
                  specialKeyButton "Tab" "{TAB}"
                  specialKeyButton "Enter" "{ENTER}"
                  specialKeyButton "Backspace" "{BACKSPACE}"
                  specialKeyButton "Delete" "{DELETE}"
              ]
              div [ _class "key-row" ] [
                  specialKeyButton "Ctrl+A" "{CTRL+A}"
                  specialKeyButton "Ctrl+C" "{CTRL+C}"
                  specialKeyButton "Ctrl+V" "{CTRL+V}"
                  specialKeyButton "Ctrl+X" "{CTRL+X}"
                  specialKeyButton "Ctrl+Z" "{CTRL+Z}"
                  specialKeyButton "Win" "{WIN}"
              ]
              div [ _class "key-row" ] [
                  div [ _class "arrow-pad" ] [
                      div [ _class "arrow-row" ] [
                          specialKeyButton "Up" "{UP}"
                      ]
                      div [ _class "arrow-row" ] [
                          specialKeyButton "Left" "{LEFT}"
                          specialKeyButton "Down" "{DOWN}"
                          specialKeyButton "Right" "{RIGHT}"
                      ]
                  ]
              ]
          ]
          p [ _class "hint" ]
              [ str "Special keys: "
                code [] [ str "{BACKSPACE}" ]
                str " "
                code [] [ str "{ENTER}" ]
                str " "
                code [] [ str "{TAB}" ]
                str " "
                code [] [ str "{WIN}" ]
                str ". Use "
                code [] [ str "{{" ]
                str " and "
                code [] [ str "}}" ]
                str " for literal braces." ]
        ]

    html [] [
        head [] [
            title [] [ str "LinuxKey Sender" ]
            meta [ _charset "utf-8" ]
            meta [ _name "viewport"; _content "width=device-width, initial-scale=1" ]
            style [] [
                rawText
                    """
@import url('https://fonts.googleapis.com/css2?family=Manrope:wght@400;600;700&family=JetBrains+Mono:wght@400;600&display=swap');
:root {
  --bg: #f7f4ee;
  --panel: #ffffff;
  --text: #0f172a;
  --muted: #475569;
  --accent: #0f766e;
  --accent-dark: #0b5f58;
  --secondary: #475569;
  --border: #cbd5e1;
  --success: #16a34a;
  --danger: #dc2626;
  --warning: #d97706;
  --unknown: #64748b;
}
body { font-family: 'Manrope', 'Segoe UI', sans-serif; margin: 2rem auto; max-width: 48rem; color: var(--text); background: var(--bg); padding: 0 1.5rem; }
h1 { color: var(--accent); letter-spacing: 0.02em; }
form { margin-top: 1.5rem; display: flex; flex-direction: column; gap: 1rem; }
textarea { min-height: 12rem; padding: 1rem; font-size: 1rem; font-family: 'JetBrains Mono', Consolas, monospace; border: 1px solid var(--border); border-radius: 0.5rem; background: var(--panel); color: inherit; }
button { padding: 0.75rem 1.5rem; border-radius: 0.5rem; border: none; font-size: 1rem; cursor: pointer; }
button.primary { background: var(--accent); color: white; align-self: flex-start; }
button.primary:hover { background: var(--accent-dark); }
button.secondary { background: var(--secondary); color: white; }
button.secondary:hover { background: #334155; }
.hint { margin: 0; font-size: 0.85rem; color: #64748b; line-height: 1.4; }
.hint code { background: #e2e8f0; padding: 0.1rem 0.35rem; border-radius: 0.25rem; font-family: 'JetBrains Mono', Consolas, monospace; }
.alert { padding: 0.75rem 1rem; border-radius: 0.5rem; border: 1px solid transparent; }
.alert.success { background: #dcfce7; border-color: #86efac; color: #166534; }
.alert.error { background: #fee2e2; border-color: #fca5a5; color: #991b1b; }
.target { font-size: 0.95rem; color: var(--muted); }
.layout-row { display: flex; align-items: center; gap: 0.75rem; flex-wrap: wrap; }
.layout-row label { font-weight: 600; }
.layout-row select { padding: 0.4rem 0.6rem; border-radius: 0.4rem; border: 1px solid var(--border); background: var(--panel); }
.status-panel { position: fixed; top: 1rem; right: 1rem; display: flex; align-items: center; gap: 0.45rem; background: rgba(255, 255, 255, 0.9); padding: 0.35rem 0.6rem; border-radius: 999px; border: 1px solid var(--border); box-shadow: 0 12px 24px rgba(15, 23, 42, 0.08); z-index: 10; backdrop-filter: blur(6px); }
.status-panel button { padding: 0.35rem 0.7rem; font-size: 0.8rem; }
.usb-dot { width: 0.7rem; height: 0.7rem; border-radius: 50%; background: var(--unknown); box-shadow: 0 0 0 3px rgba(100, 116, 139, 0.2); }
.usb-dot.connected { background: var(--success); box-shadow: 0 0 0 3px rgba(22, 163, 74, 0.2); }
.usb-dot.disconnected { background: var(--danger); box-shadow: 0 0 0 3px rgba(220, 38, 38, 0.2); }
.usb-dot.pending { background: var(--warning); box-shadow: 0 0 0 3px rgba(217, 119, 6, 0.2); }
.usb-dot.unknown { background: var(--unknown); box-shadow: 0 0 0 3px rgba(100, 116, 139, 0.2); }
.special-keys { display: flex; flex-direction: column; gap: 0.6rem; }
.key-row { display: flex; gap: 0.5rem; flex-wrap: wrap; align-items: center; }
.special-keys button { padding: 0.35rem 0.6rem; font-size: 0.85rem; }
.arrow-pad { display: flex; flex-direction: column; align-items: center; gap: 0.35rem; }
.arrow-row { display: flex; gap: 0.4rem; justify-content: center; }
.send { margin-top: 0.1rem; }
body.mobile { margin: 1rem auto; padding: 0 1rem; }
body.mobile h1 { font-size: 1.6rem; }
body.mobile textarea { min-height: 10rem; font-size: 1rem; }
body.mobile button.primary { width: 100%; align-self: stretch; }
@media (max-width: 720px) {
  body { margin: 1rem auto; padding: 0 1rem; }
  h1 { font-size: 1.6rem; }
  textarea { min-height: 10rem; font-size: 1rem; }
  button.primary { width: 100%; align-self: stretch; }
  .status-panel { top: 0.6rem; right: 0.6rem; }
}
"""
            ]
            script [] [
                rawText
                    """
document.addEventListener('DOMContentLoaded', () => {
  const statusEl = document.getElementById('usb-status');
  const refreshBtn = document.getElementById('refresh-status');

  const setStatus = (text, cssClass) => {
    if (!statusEl) return;
    statusEl.className = `usb-dot ${cssClass || 'unknown'}`;
    statusEl.setAttribute('title', text);
    statusEl.setAttribute('aria-label', text);
  };

  const refreshStatus = () => {
    if (!statusEl) return;
    if (refreshBtn) refreshBtn.disabled = true;
    fetch('/status', { cache: 'no-store' })
      .then((resp) => resp.ok ? resp.json() : Promise.reject(resp.status))
      .then((data) => setStatus(data.text || 'Raspberry Pi USB: unknown', data.cssClass || 'unknown'))
      .catch(() => setStatus('Raspberry Pi USB: unknown (refresh failed)', 'unknown'))
      .finally(() => { if (refreshBtn) refreshBtn.disabled = false; });
  };

  if (refreshBtn) {
    refreshBtn.addEventListener('click', (event) => {
      event.preventDefault();
      refreshStatus();
    });
  }

  const textarea = document.getElementById('text');
  const insertToken = (token) => {
    if (!textarea || !token) return;
    const start = textarea.selectionStart ?? textarea.value.length;
    const end = textarea.selectionEnd ?? textarea.value.length;
    textarea.value = `${textarea.value.slice(0, start)}${token}${textarea.value.slice(end)}`;
    const caret = start + token.length;
    textarea.setSelectionRange(caret, caret);
    textarea.focus();
  };

  document.querySelectorAll('[data-token]').forEach((button) => {
    button.addEventListener('click', (event) => {
      event.preventDefault();
      insertToken(button.getAttribute('data-token'));
    });
  });
});
"""
            ]
        ]
        body bodyAttrs
            ([ div [ _class "status-panel" ] [
                   span [ _id "usb-status"
                          _class (sprintf "usb-dot %s" model.UsbStatus.CssClass)
                          _title model.UsbStatus.Text
                          attr "aria-label" model.UsbStatus.Text ] []
                   button [ _type "button"; _id "refresh-status"; _class "secondary" ] [ str "Refresh" ]
               ]
               h1 [] [ str "LinuxKey Sender" ]
               p [ _class "target" ] [ str (sprintf "Target device: %s:%d" settings.TargetIp settings.TargetPort) ]
            ]
             @ statusNodes
             @ [ form
                     [ _method "post"; _action "/send" ]
                     formNodes ])
    ]
    |> htmlView

let indexHandler settings : HttpHandler =
    fun next ctx ->
        let layout = getDefaultLayout ()
        let model =
            { Status = Idle
              Text = ""
              UsbStatus = readUsbStatus ()
              IsMobile = isMobileClient ctx
              Layout = layout }
        renderPage settings model next ctx

let sendHandler (settings: SenderSettings) : HttpHandler =
    fun next ctx ->
        task {
            let isMobile = isMobileClient ctx
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

                return! (renderPage settings model) next ctx
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

                    return! (renderPage settings model) next ctx
                | Error message ->
                    let model =
                        { Status = Failure message
                          Text = text
                          UsbStatus = readUsbStatus ()
                          IsMobile = isMobile
                          Layout = layout }

                    return! (renderPage settings model) next ctx
        }

let webApp settings =
    choose [
        GET >=> route "/" >=> indexHandler settings
        POST >=> route "/send" >=> sendHandler settings
        GET >=> route "/status" >=> statusHandler
        GET >=> route "/healthz" >=> text "OK"
    ]

let sendOnce (ip: string) (port: string) (text: string) =
    match tryParsePort port with
    | Some parsedPort ->
        try
            use client = new TcpClient()
            client.Connect(ip, parsedPort)
            use stream = client.GetStream()
            let payload = Encoding.UTF8.GetBytes text
            stream.Write(payload, 0, payload.Length)
            printfn "Sent %d bytes to %s:%d" payload.Length ip parsedPort
            0
        with ex ->
            eprintfn "Failed: %s" ex.Message
            1
    | None ->
        eprintfn "Invalid port '%s'." port
        1

let startWebServer (argv: string[]) (ip: string) (port: int) =
    let settings = { TargetIp = ip; TargetPort = port }

    let webPort =
        match envOrDefault "SENDER_WEB_PORT" "8080" |> tryParsePort with
        | Some value -> value
        | None ->
            eprintfn "Environment variable SENDER_WEB_PORT is not a valid port. Falling back to 8080."
            8080

    let urls =
        match envOrDefault "SENDER_WEB_URLS" "" with
        | "" -> [ sprintf "http://0.0.0.0:%d" webPort ]
        | value ->
            value.Split(';', StringSplitOptions.RemoveEmptyEntries)
            |> Array.toList

    let builder =
        Host
            .CreateDefaultBuilder(argv)
            .ConfigureServices(fun _ services ->
                services.AddSingleton(settings) |> ignore
                services.AddGiraffe() |> ignore)
            .ConfigureWebHostDefaults(fun webHostBuilder ->
                webHostBuilder
                    .UseUrls(urls |> List.toArray)
                    .Configure(fun app ->
                        app.UseGiraffe(webApp settings))
                |> ignore)

    try
        use host = builder.Build()
        printfn "Web UI running at %s" (String.Join(", ", urls))
        printfn "Sending to %s:%d" settings.TargetIp settings.TargetPort
        host.Run()
        0
    with ex ->
        eprintfn "Failed to start web server: %s" ex.Message
        1

let usage () =
    printfn "Usage:"
    printfn "  dotnet run -- <target-ip> <port> \"text\"     # send a single payload"
    printfn "  dotnet run -- [target-ip] [port]              # start the web UI (defaults to env or 127.0.0.1:5000)"
    printfn ""
    printfn "Environment variables:"
    printfn "  SENDER_TARGET_IP         Default target IP (default: 127.0.0.1)"
    printfn "  SENDER_TARGET_PORT       Default target port (default: 5000)"
    printfn "  SENDER_WEB_PORT          Port for the web UI (default: 8080)"
    printfn "  SENDER_WEB_URLS          Semicolon-delimited list of URLs for the web UI (overrides port)"
    printfn "  SENDER_USB_STATE_PATH    Optional UDC state file path (default: /sys/class/udc/*/state)"
    printfn "  SENDER_LAYOUT            Default layout for the UI (en or de, default: en)"
    printfn "  SENDER_LAYOUT_TOKEN      Prefix outgoing text with {LAYOUT=...} (default: false)"
    1

[<EntryPoint>]
let main argv =
    match argv with
    | [| "--help" |] | [| "-h" |] -> usage ()
    | [| ip; port; text |] -> sendOnce ip port text
    | _ ->
        let defaultIp = envOrDefault "SENDER_TARGET_IP" "127.0.0.1"
        let defaultPort =
            envOrDefault "SENDER_TARGET_PORT" "5000"
            |> fun value -> defaultArg (tryParsePort value) 5000

        let targetIp =
            if argv.Length >= 1 && not (String.IsNullOrWhiteSpace argv.[0]) then
                argv.[0]
            else
                defaultIp

        let portValue =
            if argv.Length >= 2 then
                argv.[1]
            else
                defaultPort.ToString()

        match tryParsePort portValue with
        | Some targetPort -> startWebServer argv targetIp targetPort
        | None ->
            eprintfn "Invalid port '%s'." portValue
            usage ()
