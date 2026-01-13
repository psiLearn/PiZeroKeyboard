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

        if parts.Length = 0 then
            None
        else
            let mutable modifier = 0uy
            let mutable keyOpt: HidKey option = None
            let mutable invalid = false

            let setKey (hid: HidKey) =
                match keyOpt with
                | Some _ -> invalid <- true
                | None -> keyOpt <- Some hid

            for part in parts do
                match HidMapping.tryGetModifierToken part with
                | Some modValue ->
                    modifier <- modifier ||| modValue
                | None ->
                    match HidMapping.tryGetSpecialToken part with
                    | Some hid when hid.Key <> 0uy ->
                        setKey hid
                    | Some hid ->
                        modifier <- modifier ||| hid.Modifier
                    | None ->
                        if part.Length = 1 then
                            let ch = part.[0]
                            let normalized =
                                if Char.IsLetter ch then
                                    Char.ToLowerInvariant ch
                                else
                                    ch

                            match HidMapping.toHid layout normalized with
                            | Some hid -> setKey hid
                            | None -> invalid <- true
                        else
                            invalid <- true

            if invalid then
                None
            else
                match keyOpt with
                | Some hid -> Some { hid with Modifier = hid.Modifier ||| modifier }
                | None when modifier <> 0uy -> Some { Modifier = modifier; Key = 0uy }
                | None -> None

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
                let endIdx = text.IndexOf('}', i + 1)
                if endIdx = -1 then
                    tokens.Add(TextChar '{')
                    i <- i + 1
                else
                    let token = text.Substring(i + 1, endIdx - i - 1)
                    let parsed =
                        match tryParseLayoutToken token with
                        | Some layout -> Some (LayoutSwitch layout)
                        | None ->
                            if token.Contains "+" then
                                Some (ChordToken token)
                            else
                                HidMapping.tryGetSpecialToken token |> Option.map SpecialKey

                    match parsed with
                    | Some parsedToken ->
                        tokens.Add(parsedToken)
                        i <- endIdx + 1
                    | None ->
                        tokens.Add(TextChar '{')
                        for ch in token do
                            tokens.Add(TextChar ch)
                        tokens.Add(TextChar '}')
                        i <- endIdx + 1
            | ch ->
                tokens.Add(TextChar ch)
                i <- i + 1
        tokens

    let processText (send: HidKey -> unit) (logUnsupported: char -> unit) layout (text: string) =
        if isNull text then
            invalidArg (nameof text) "Text cannot be null."

        let mutable currentLayout = layout

        let sendTextChar c =
            match HidMapping.toHid currentLayout c with
            | Some hid -> send hid
            | None -> logUnsupported c

        tokenize text
        |> Seq.iter (fun token ->
            match token with
            | LayoutSwitch newLayout -> currentLayout <- newLayout
            | SpecialKey hid -> send hid
            | ChordToken tokenText ->
                match tryParseChord currentLayout tokenText with
                | Some hid -> send hid
                | None ->
                    for ch in "{" + tokenText + "}" do
                        sendTextChar ch
            | TextChar c -> sendTextChar c)
