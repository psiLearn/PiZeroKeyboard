namespace ReceiverApp

open System
open ReceiverApp.HidMapping

module TextProcessor =
    let processText (send: HidKey -> unit) (logUnsupported: char -> unit) (text: string) =
        if isNull text then
            invalidArg (nameof text) "Text cannot be null."

        text
        |> Seq.iter (fun c ->
            match HidMapping.toHid c with
            | Some hid -> send hid
            | None -> logUnsupported c)
