namespace SenderApp

module UsbStatusService =
    open System
    open System.IO
    open SenderApp.Configuration
    open SenderApp.UsbStatusModel

    let tryGetUsbStatePath () =
        let configuredPath = envOrDefault "SENDER_USB_STATE_PATH" ""
        if not (String.IsNullOrWhiteSpace configuredPath) && File.Exists configuredPath then
            Some configuredPath
        elif Directory.Exists "/sys/class/udc" then
            let entries = Directory.GetDirectories "/sys/class/udc"
            if entries.Length > 0 then
                Some(Path.Combine(entries.[0], "state"))
            else
                None
        else
            None

    let getUsbEventPath () =
        let eventPath = envOrDefault "SENDER_USB_EVENT_PATH" ""
        if String.IsNullOrWhiteSpace eventPath then
            None
        else
            Some eventPath

    let readUsbStatus () =
        match tryGetUsbStatePath () with
        | None -> statusMissingStateFile ()
        | Some path ->
            try
                File.ReadAllText(path) |> statusFromState
            with ex ->
                statusReadError ex.Message
