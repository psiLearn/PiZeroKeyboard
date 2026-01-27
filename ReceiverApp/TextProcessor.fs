namespace ReceiverApp

open System
open ReceiverApp.HidMapping

module TextProcessor =
    type private ParsedToken =
        | TextChar of char
        | SpecialKey of HidKey
        | LayoutSwitch of HidMapping.KeyboardLayout
        | ChordToken of string

    let private tryParseLayoutToken (token: string) =
        let trimmed = token.Trim()
        if trimmed.StartsWith("LAYOUT=", StringComparison.OrdinalIgnoreCase) then
            let value = trimmed.Substring("LAYOUT=".Length)
            HidMapping.tryParseLayout value
        elif trimmed.StartsWith("LAYOUT:", StringComparison.OrdinalIgnoreCase) then
            let value = trimmed.Substring("LAYOUT:".Length)
            HidMapping.tryParseLayout value
        else
            None

    let private tryParseChord layout (token: string) =
        let parts =
            token.Split('+', StringSplitOptions.RemoveEmptyEntries)
            |> Array.map (fun part -> part.Trim())
            |> Array.filter (fun part -> part.Length > 0)

        if parts.Length = 0 then None
        else
            let processChordPart layout (modifier, keyOpt, invalid) part =
                match HidMapping.tryGetModifierToken part with
                | Some modValue -> (modifier ||| modValue, keyOpt, invalid)
                | None ->
                    match HidMapping.tryGetSpecialToken part with
                    | Some hid when hid.Key <> 0uy ->
                        if Option.isSome keyOpt then (modifier, keyOpt, true)
                        else (modifier, Some hid, invalid)
                    | Some hid ->
                        (modifier ||| hid.Modifier, keyOpt, invalid)
                    | None ->
                        if part.Length = 1 then
                            let ch = part.[0]
                            let normalized = if Char.IsLetter ch then Char.ToLowerInvariant ch else ch
                            match HidMapping.toHid layout normalized with
                            | Some hid ->
                                if Option.isSome keyOpt then (modifier, keyOpt, true)
                                else (modifier, Some hid, invalid)
                            | None -> (modifier, keyOpt, true)
                        else (modifier, keyOpt, true)

            let (modifier, keyOpt, invalid) = 
                Array.fold (processChordPart layout) (0uy, None, false) parts

            match invalid, keyOpt, modifier with
            | true, _, _ -> None
            | false, Some hid, _ -> Some { hid with Modifier = hid.Modifier ||| modifier }
            | false, None, m when m <> 0uy -> Some { Modifier = m; Key = 0uy }
            | _ -> None

    let private tryParseToken token =
        match tryParseLayoutToken token with
        | Some layout -> Some (LayoutSwitch layout)
        | None ->
            if token.Contains "+" then Some (ChordToken token)
            else HidMapping.tryGetSpecialToken token |> Option.map SpecialKey

    let private handleBracketToken (tokens: ResizeArray<ParsedToken>) token endIdx =
        match tryParseToken token with
        | Some parsedToken ->
            tokens.Add(parsedToken)
            endIdx + 1
        | None ->
            tokens.Add(TextChar '{')
            for ch in token do
                tokens.Add(TextChar ch)
            tokens.Add(TextChar '}')
            endIdx + 1

    let private tokenize (text: string) =
        let tokens = ResizeArray<ParsedToken>()
        let mutable i = 0
        while i < text.Length do
            match text.[i] with
            | '{' when i + 1 < text.Length && text.[i + 1] = '{' ->
                tokens.Add(TextChar '{')
                i <- i + 2
            | '}' when i + 1 < text.Length && text.[i + 1] = '}' ->
                tokens.Add(TextChar '}')
                i <- i + 2
            | '{' ->
                match text.IndexOf('}', i + 1) with
                | -1 ->
                    tokens.Add(TextChar '{')
                    i <- i + 1
                | endIdx ->
                    let token = text.Substring(i + 1, endIdx - i - 1)
                    i <- handleBracketToken tokens token endIdx
            | ch ->
                tokens.Add(TextChar ch)
                i <- i + 1
        tokens

    let processText (send: HidKey -> unit) (logUnsupported: char -> unit) layout (text: string) =
        if isNull text then
            invalidArg (nameof text) "Text cannot be null."

        let sendTextChar currentLayout c =
            match HidMapping.toHid currentLayout c with
            | Some hid -> send hid
            | None -> logUnsupported c

        let sendLiteralToken currentLayout tokenText =
            for ch in "{" + tokenText + "}" do
                sendTextChar currentLayout ch

        let handleToken currentLayout token =
            match token with
            | LayoutSwitch newLayout -> newLayout
            | SpecialKey hid ->
                send hid
                currentLayout
            | ChordToken tokenText ->
                match tryParseChord currentLayout tokenText with
                | Some hid -> send hid
                | None -> sendLiteralToken currentLayout tokenText
                currentLayout
            | TextChar c ->
                sendTextChar currentLayout c
                currentLayout

        tokenize text |> Seq.fold handleToken layout |> ignore
