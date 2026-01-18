namespace SenderApp

module Cli =
    open System
    open SenderApp.Configuration
    open SenderApp.ReceiverClient
    open SenderApp.Startup

    let sendOnceCli (ip: string) (port: string) (text: string) =
        match tryParsePort port with
        | Some parsedPort -> sendOnce ip parsedPort text
        | None ->
            eprintfn "Invalid port '%s'." port
            1

    let run (argv: string[]) =
        match argv with
        | [| "--help" |] | [| "-h" |] -> usage ()
        | [| ip; port; text |] -> sendOnceCli ip port text
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
