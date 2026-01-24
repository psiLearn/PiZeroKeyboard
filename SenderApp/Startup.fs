namespace SenderApp

module Startup =
    open System
    open Microsoft.AspNetCore.Builder
    open Microsoft.AspNetCore.Hosting
    open Microsoft.AspNetCore.Server.Kestrel.Https
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

        let httpUrls =
            match envOrDefault "SENDER_WEB_URLS" "" with
            | "" -> [ sprintf "http://0.0.0.0:%d" webPort ]
            | value ->
                value.Split(';', StringSplitOptions.RemoveEmptyEntries)
                |> Array.toList

        let httpsCertPath = envOrDefault "SENDER_HTTPS_CERT_PATH" ""
        let httpsCertPassword = envOrDefault "SENDER_HTTPS_CERT_PASSWORD" ""

        let httpsPort =
            match envOrDefault "SENDER_HTTPS_PORT" "8443" |> tryParsePort with
            | Some value -> value
            | None ->
                eprintfn "Environment variable SENDER_HTTPS_PORT is not a valid port. Falling back to 8443."
                8443

        let enableHttps = not (String.IsNullOrWhiteSpace httpsCertPath)

        let displayUrls =
            if enableHttps then
                [ sprintf "https://0.0.0.0:%d" httpsPort ]
            else
                httpUrls

        let builder =
            Host
                .CreateDefaultBuilder(argv)
                .ConfigureServices(fun _ services ->
                    services.AddSingleton(settings) |> ignore
                    services.AddGiraffe() |> ignore)
                .ConfigureWebHostDefaults(fun webHostBuilder ->
                    let configuredBuilder =
                        if enableHttps then
                            webHostBuilder.ConfigureKestrel(fun options ->
                                options.ListenAnyIP(
                                    httpsPort,
                                    fun listenOptions ->
                                        if String.IsNullOrWhiteSpace httpsCertPassword then
                                            listenOptions.UseHttps(httpsCertPath) |> ignore
                                        else
                                            listenOptions.UseHttps(httpsCertPath, httpsCertPassword) |> ignore)
                                |> ignore)
                        else
                            webHostBuilder.UseUrls(httpUrls |> List.toArray)

                    configuredBuilder.Configure(fun app ->
                        app.UseStaticFiles() |> ignore
                        app.UseGiraffe(webApp settings))
                    |> ignore)

        try
            use host = builder.Build()
            printfn "Web UI running at %s" (String.Join(", ", displayUrls))
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
        printfn "  SENDER_HTTPS_CERT_PATH   Path to a PFX certificate to enable HTTPS"
        printfn "  SENDER_HTTPS_CERT_PASSWORD  Password for the PFX certificate (optional)"
        printfn "  SENDER_HTTPS_PORT        HTTPS port (default: 8443 when HTTPS is enabled)"
        printfn "                           When HTTPS is enabled, HTTP is disabled."
        printfn "  SENDER_USB_STATE_PATH    Optional UDC state file path (default: /sys/class/udc/*/state)"
        printfn "  SENDER_CAPSLOCK_PATH     Optional Caps Lock status file (default: /run/linuxkey/capslock)"
        printfn "  SENDER_LAYOUT            Default layout for the UI (en or de, default: en)"
        printfn "  SENDER_LAYOUT_TOKEN      Prefix outgoing text with {LAYOUT=...} (default: false)"
        1
