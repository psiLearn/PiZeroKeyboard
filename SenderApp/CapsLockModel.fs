namespace SenderApp

module CapsLockModel =
    open System

    let statusFromValue (value: string) : CapsLockStatus =
        let normalized =
            if isNull value then
                ""
            else
                value.Trim().ToLowerInvariant()

        match normalized with
        | "on"
        | "1"
        | "true" ->
            { Text = "Caps Lock: on"
              CssClass = "on" }
        | "off"
        | "0"
        | "false" ->
            { Text = "Caps Lock: off"
              CssClass = "off" }
        | "" ->
            { Text = "Caps Lock: unknown"
              CssClass = "unknown" }
        | other ->
            { Text = sprintf "Caps Lock: %s" other
              CssClass = "unknown" }

    let statusMissingFile () : CapsLockStatus =
        { Text = "Caps Lock: unknown"
          CssClass = "unknown" }

    let statusReadError (_: string) : CapsLockStatus =
        { Text = "Caps Lock: unknown"
          CssClass = "unknown" }
