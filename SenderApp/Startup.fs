namespace SenderApp

module Startup =
    open System
    open Microsoft.AspNetCore.Builder
    open Microsoft.AspNetCore.Hosting
    open Microsoft.Extensions.DependencyInjection
    open Microsoft.Extensions.Hosting
    open Giraffe
    open SenderApp.Configuration
    open SenderApp.Routes

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
