namespace SenderApp

module Configuration =
    open System

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
