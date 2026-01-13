namespace ReceiverApp

open System

type HidKey =
    { Modifier: byte
      Key: byte }

module HidMapping =
    type KeyboardLayout =
        | En
        | De

    let private shiftMask = 0x02uy
    let private rightAltMask = 0x40uy

    let tryParseLayout (value: string) =
        match value.Trim().ToLowerInvariant() with
        | "de"
        | "de-de"
        | "german" -> Some De
        | "en"
        | "en-us"
        | "us" -> Some En
        | _ -> None

    let keyMapUs =
        [
            'a', { Modifier = 0uy; Key = 0x04uy }
            'b', { Modifier = 0uy; Key = 0x05uy }
            'c', { Modifier = 0uy; Key = 0x06uy }
            'd', { Modifier = 0uy; Key = 0x07uy }
            'e', { Modifier = 0uy; Key = 0x08uy }
            'f', { Modifier = 0uy; Key = 0x09uy }
            'g', { Modifier = 0uy; Key = 0x0Auy }
            'h', { Modifier = 0uy; Key = 0x0Buy }
            'i', { Modifier = 0uy; Key = 0x0Cuy }
            'j', { Modifier = 0uy; Key = 0x0Duy }
            'k', { Modifier = 0uy; Key = 0x0Euy }
            'l', { Modifier = 0uy; Key = 0x0Fuy }
            'm', { Modifier = 0uy; Key = 0x10uy }
            'n', { Modifier = 0uy; Key = 0x11uy }
            'o', { Modifier = 0uy; Key = 0x12uy }
            'p', { Modifier = 0uy; Key = 0x13uy }
            'q', { Modifier = 0uy; Key = 0x14uy }
            'r', { Modifier = 0uy; Key = 0x15uy }
            's', { Modifier = 0uy; Key = 0x16uy }
            't', { Modifier = 0uy; Key = 0x17uy }
            'u', { Modifier = 0uy; Key = 0x18uy }
            'v', { Modifier = 0uy; Key = 0x19uy }
            'w', { Modifier = 0uy; Key = 0x1Auy }
            'x', { Modifier = 0uy; Key = 0x1Buy }
            'y', { Modifier = 0uy; Key = 0x1Cuy }
            'z', { Modifier = 0uy; Key = 0x1Duy }
            '1', { Modifier = 0uy; Key = 0x1Euy }
            '2', { Modifier = 0uy; Key = 0x1Fuy }
            '3', { Modifier = 0uy; Key = 0x20uy }
            '4', { Modifier = 0uy; Key = 0x21uy }
            '5', { Modifier = 0uy; Key = 0x22uy }
            '6', { Modifier = 0uy; Key = 0x23uy }
            '7', { Modifier = 0uy; Key = 0x24uy }
            '8', { Modifier = 0uy; Key = 0x25uy }
            '9', { Modifier = 0uy; Key = 0x26uy }
            '0', { Modifier = 0uy; Key = 0x27uy }
            '\n', { Modifier = 0uy; Key = 0x28uy }
            '\t', { Modifier = 0uy; Key = 0x2Buy }
            ' ', { Modifier = 0uy; Key = 0x2Cuy }
            '-', { Modifier = 0uy; Key = 0x2Duy }
            '=', { Modifier = 0uy; Key = 0x2Euy }
            '[', { Modifier = 0uy; Key = 0x2Fuy }
            ']', { Modifier = 0uy; Key = 0x30uy }
            '\\', { Modifier = 0uy; Key = 0x31uy }
            ';', { Modifier = 0uy; Key = 0x33uy }
            '\'', { Modifier = 0uy; Key = 0x34uy }
            '`', { Modifier = 0uy; Key = 0x35uy }
            ',', { Modifier = 0uy; Key = 0x36uy }
            '.', { Modifier = 0uy; Key = 0x37uy }
            '/', { Modifier = 0uy; Key = 0x38uy }
            '_', { Modifier = shiftMask; Key = 0x2Duy }
            '+', { Modifier = shiftMask; Key = 0x2Euy }
            '{', { Modifier = shiftMask; Key = 0x2Fuy }
            '}', { Modifier = shiftMask; Key = 0x30uy }
            '|', { Modifier = shiftMask; Key = 0x31uy }
            ':', { Modifier = shiftMask; Key = 0x33uy }
            '"', { Modifier = shiftMask; Key = 0x34uy }
            '~', { Modifier = shiftMask; Key = 0x35uy }
            '<', { Modifier = shiftMask; Key = 0x36uy }
            '>', { Modifier = shiftMask; Key = 0x37uy }
            '?', { Modifier = shiftMask; Key = 0x38uy }
            '!', { Modifier = shiftMask; Key = 0x1Euy }
            '@', { Modifier = shiftMask; Key = 0x1Fuy }
            '#', { Modifier = shiftMask; Key = 0x20uy }
            '$', { Modifier = shiftMask; Key = 0x21uy }
            '%', { Modifier = shiftMask; Key = 0x22uy }
            '^', { Modifier = shiftMask; Key = 0x23uy }
            '&', { Modifier = shiftMask; Key = 0x24uy }
            '*', { Modifier = shiftMask; Key = 0x25uy }
            '(', { Modifier = shiftMask; Key = 0x26uy }
            ')', { Modifier = shiftMask; Key = 0x27uy }
        ]
        |> dict

    let keyMapDe =
        let map = Collections.Generic.Dictionary<char, HidKey>()
        for KeyValue(key, value) in keyMapUs do
            map.[key] <- value

        let overrides =
            [
                'y', { Modifier = 0uy; Key = 0x1Duy }
                'z', { Modifier = 0uy; Key = 0x1Cuy }
                '\u00e4', { Modifier = 0uy; Key = 0x34uy }
                '\u00f6', { Modifier = 0uy; Key = 0x33uy }
                '\u00fc', { Modifier = 0uy; Key = 0x2Fuy }
                '\u00df', { Modifier = 0uy; Key = 0x2Duy }
                '-', { Modifier = 0uy; Key = 0x38uy }
                '_', { Modifier = shiftMask; Key = 0x38uy }
                '/', { Modifier = shiftMask; Key = 0x24uy }
                '?', { Modifier = shiftMask; Key = 0x2Duy }
                '+', { Modifier = 0uy; Key = 0x30uy }
                '*', { Modifier = shiftMask; Key = 0x30uy }
                ';', { Modifier = shiftMask; Key = 0x36uy }
                ':', { Modifier = shiftMask; Key = 0x37uy }
                '"', { Modifier = shiftMask; Key = 0x1Fuy }
                '\'', { Modifier = shiftMask; Key = 0x32uy }
                '#', { Modifier = 0uy; Key = 0x32uy }
                '@', { Modifier = rightAltMask; Key = 0x14uy }
                '[', { Modifier = rightAltMask; Key = 0x25uy }
                ']', { Modifier = rightAltMask; Key = 0x26uy }
                '{', { Modifier = rightAltMask; Key = 0x24uy }
                '}', { Modifier = rightAltMask; Key = 0x27uy }
                '\\', { Modifier = rightAltMask; Key = 0x2Duy }
                '|', { Modifier = rightAltMask; Key = 0x64uy }
                '<', { Modifier = 0uy; Key = 0x64uy }
                '>', { Modifier = shiftMask; Key = 0x64uy }
                '=', { Modifier = shiftMask; Key = 0x27uy }
                '^', { Modifier = 0uy; Key = 0x35uy }
                '&', { Modifier = shiftMask; Key = 0x23uy }
                '\u20ac', { Modifier = rightAltMask; Key = 0x08uy }
                '\u00a7', { Modifier = shiftMask; Key = 0x20uy }
            ]

        for key, value in overrides do
            map.[key] <- value

        map :> Collections.Generic.IDictionary<char, HidKey>

    let modifierMap =
        [
            "WIN", 0x08uy
            "LWIN", 0x08uy
            "RWIN", 0x80uy
            "CTRL", 0x01uy
            "LCTRL", 0x01uy
            "RCTRL", 0x10uy
            "ALT", 0x04uy
            "LALT", 0x04uy
            "RALT", 0x40uy
            "ALTGR", 0x40uy
            "SHIFT", 0x02uy
            "LSHIFT", 0x02uy
            "RSHIFT", 0x20uy
        ]
        |> dict

    let specialKeyMap =
        [
            "BACKSPACE", { Modifier = 0uy; Key = 0x2Auy }
            "BKSP", { Modifier = 0uy; Key = 0x2Auy }
            "ENTER", { Modifier = 0uy; Key = 0x28uy }
            "TAB", { Modifier = 0uy; Key = 0x2Buy }
            "ESC", { Modifier = 0uy; Key = 0x29uy }
            "ESCAPE", { Modifier = 0uy; Key = 0x29uy }
            "DEL", { Modifier = 0uy; Key = 0x4Cuy }
            "DELETE", { Modifier = 0uy; Key = 0x4Cuy }
            "UP", { Modifier = 0uy; Key = 0x52uy }
            "DOWN", { Modifier = 0uy; Key = 0x51uy }
            "LEFT", { Modifier = 0uy; Key = 0x50uy }
            "RIGHT", { Modifier = 0uy; Key = 0x4Fuy }
            "HOME", { Modifier = 0uy; Key = 0x4Auy }
            "END", { Modifier = 0uy; Key = 0x4Duy }
            "PAGEUP", { Modifier = 0uy; Key = 0x4Buy }
            "PAGEDOWN", { Modifier = 0uy; Key = 0x4Euy }
        ]
        |> dict

    let private tryLookup layout (c: char) =
        let map =
            match layout with
            | En -> keyMapUs
            | De -> keyMapDe

        match map.TryGetValue c with
        | true, entry -> Some entry
        | _ -> None

    let tryGetModifierToken (token: string) =
        let key = token.Trim().ToUpperInvariant()
        match modifierMap.TryGetValue key with
        | true, value -> Some value
        | _ -> None

    let tryGetSpecialToken (token: string) =
        let key = token.Trim().ToUpperInvariant()
        match modifierMap.TryGetValue key with
        | true, value -> Some { Modifier = value; Key = 0uy }
        | _ ->
            match specialKeyMap.TryGetValue key with
            | true, entry -> Some entry
            | _ -> None

    let toHid layout (c: char) =
        let normalized =
            match c with
            | '\r' -> '\n'
            | other -> other

        if Char.IsLetter normalized && Char.IsUpper normalized then
            let lower = Char.ToLowerInvariant normalized
            match tryLookup layout lower with
            | Some baseEntry ->
                Some { baseEntry with Modifier = baseEntry.Modifier ||| shiftMask }
            | None -> None
        else
            tryLookup layout normalized
