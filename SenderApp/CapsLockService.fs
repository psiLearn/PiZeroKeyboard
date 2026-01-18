namespace SenderApp

module CapsLockService =
    open System
    open System.IO
    open SenderApp.Configuration
    open SenderApp.CapsLockModel

    let getCapsLockPath () =
        let path = envOrDefault "SENDER_CAPSLOCK_PATH" "/run/linuxkey/capslock"
        if String.IsNullOrWhiteSpace path then
            None
        else
            Some path

    let readCapsLockStatus () =
        match getCapsLockPath () with
        | None -> statusMissingFile ()
        | Some path ->
            if not (File.Exists path) then
                statusMissingFile ()
            else
                try
                    File.ReadAllText(path) |> statusFromValue
                with ex ->
                    statusReadError ex.Message
