namespace ReceiverApp

open System

type HidKey =
    { Modifier: byte
      Key: byte }

module HidMapping =
    let private uppercaseMask = 0x02uy

    let keyMap =
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
            '_', { Modifier = 0x02uy; Key = 0x2Duy }
            '+', { Modifier = 0x02uy; Key = 0x2Euy }
            '{', { Modifier = 0x02uy; Key = 0x2Fuy }
            '}', { Modifier = 0x02uy; Key = 0x30uy }
            '|', { Modifier = 0x02uy; Key = 0x31uy }
            ':', { Modifier = 0x02uy; Key = 0x33uy }
            '"', { Modifier = 0x02uy; Key = 0x34uy }
            '~', { Modifier = 0x02uy; Key = 0x35uy }
            '<', { Modifier = 0x02uy; Key = 0x36uy }
            '>', { Modifier = 0x02uy; Key = 0x37uy }
            '?', { Modifier = 0x02uy; Key = 0x38uy }
            '!', { Modifier = 0x02uy; Key = 0x1Euy }
            '@', { Modifier = 0x02uy; Key = 0x1Fuy }
            '#', { Modifier = 0x02uy; Key = 0x20uy }
            '$', { Modifier = 0x02uy; Key = 0x21uy }
            '%', { Modifier = 0x02uy; Key = 0x22uy }
            '^', { Modifier = 0x02uy; Key = 0x23uy }
            '&', { Modifier = 0x02uy; Key = 0x24uy }
            '*', { Modifier = 0x02uy; Key = 0x25uy }
            '(', { Modifier = 0x02uy; Key = 0x26uy }
            ')', { Modifier = 0x02uy; Key = 0x27uy }
        ]
        |> dict

    let private tryLookup (c: char) =
        match keyMap.TryGetValue c with
        | true, entry -> Some entry
        | _ -> None

    let toHid (c: char) =
        let normalized =
            match c with
            | '\r' -> '\n'
            | other -> other

        if Char.IsLetter normalized && Char.IsUpper normalized then
            let lower = Char.ToLowerInvariant normalized
            match tryLookup lower with
            | Some baseEntry ->
                Some { baseEntry with Modifier = baseEntry.Modifier ||| uppercaseMask }
            | None -> None
        else
            tryLookup normalized
