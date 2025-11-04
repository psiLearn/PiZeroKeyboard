open System
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

type IndexViewModel =
    { Status: SendStatus
      Text: string }

[<CLIMutable>]
type SendRequest =
    { Text: string }

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
    let statusNodes =
        match model.Status with
        | Idle -> []
        | Success bytes ->
            [ div
                  [ _class "alert success" ]
                  [ str (sprintf "Sent %d bytes to %s:%d." bytes settings.TargetIp settings.TargetPort) ] ]
        | Failure message ->
            [ div
                  [ _class "alert error" ]
                  [ str (sprintf "Failed to send text: %s" message) ] ]

    html [] [
        head [] [
            title [] [ str "LinuxKey Sender" ]
            meta [ _charset "utf-8" ]
            style [] [
                rawText
                    """
body { font-family: 'Segoe UI', sans-serif; margin: 2rem auto; max-width: 48rem; color: #0f172a; background: #f8fafc; padding: 0 1.5rem; }
h1 { color: #0284c7; }
form { margin-top: 1.5rem; display: flex; flex-direction: column; gap: 1rem; }
textarea { min-height: 12rem; padding: 1rem; font-size: 1rem; font-family: 'Fira Code', Consolas, monospace; border: 1px solid #cbd5e1; border-radius: 0.5rem; background: white; color: inherit; }
button { padding: 0.75rem 1.5rem; border-radius: 0.5rem; border: none; background: #0284c7; color: white; font-size: 1rem; cursor: pointer; align-self: flex-start; }
button:hover { background: #0369a1; }
.alert { padding: 0.75rem 1rem; border-radius: 0.5rem; border: 1px solid transparent; }
.alert.success { background: #dcfce7; border-color: #86efac; color: #166534; }
.alert.error { background: #fee2e2; border-color: #fca5a5; color: #991b1b; }
.target { font-size: 0.95rem; color: #475569; }
"""
            ]
        ]
        body []
            ([ h1 [] [ str "LinuxKey Sender" ]
               p [ _class "target" ] [ str (sprintf "Target device: %s:%d" settings.TargetIp settings.TargetPort) ] ]
             @ statusNodes
             @ [ form
                     [ _method "post"; _action "/send" ]
                     [ textarea
                           [ _name "text"; _placeholder "Paste text here..." ]
                           [ str model.Text ]
                       button [ _type "submit" ] [ str "Send" ] ] ])
    ]
    |> htmlView

let indexHandler settings : HttpHandler =
    renderPage settings { Status = Idle; Text = "" }

let sendHandler (settings: SenderSettings) : HttpHandler =
    fun next ctx ->
        task {
            let! form = ctx.BindFormAsync<SendRequest>()

            let text =
                form.Text
                |> Option.ofObj
                |> Option.defaultValue ""
                |> fun t -> t.TrimEnd()

            if String.IsNullOrWhiteSpace text then
                let model =
                    { Status = Failure "Please enter some text before sending."
                      Text = "" }

                return! (renderPage settings model) next ctx
            else
                let logger = ctx.GetLogger()
                let! result = sendToReceiver logger settings text

                match result with
                | Ok bytes ->
                    let model =
                        { Status = Success bytes
                          Text = "" }

                    return! (renderPage settings model) next ctx
                | Error message ->
                    let model =
                        { Status = Failure message
                          Text = text }

                    return! (renderPage settings model) next ctx
        }

let webApp settings =
    choose [
        GET >=> route "/" >=> indexHandler settings
        POST >=> route "/send" >=> sendHandler settings
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
