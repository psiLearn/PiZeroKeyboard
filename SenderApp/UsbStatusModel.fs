namespace SenderApp

module UsbStatusModel =
    open System

    let statusFromState (state: string) =
        let normalized =
            if isNull state then
                ""
            else
                state.Trim().ToLowerInvariant()

        match normalized with
        | "configured" ->
            { Text = "Raspberry Pi USB: connected (configured)"
              CssClass = "connected" }
        | "not attached" ->
            { Text = "Raspberry Pi USB: not attached"
              CssClass = "disconnected" }
        | "attached"
        | "powered"
        | "default" ->
            { Text = sprintf "Raspberry Pi USB: %s (not configured yet)" normalized
              CssClass = "pending" }
        | "" ->
            { Text = "Raspberry Pi USB: unknown (empty state)"
              CssClass = "unknown" }
        | other ->
            { Text = sprintf "Raspberry Pi USB: %s" other
              CssClass = "unknown" }

    let statusMissingStateFile () =
        { Text = "Raspberry Pi USB: unknown (state file not found)"
          CssClass = "unknown" }

    let statusReadError (message: string) =
        { Text = sprintf "Raspberry Pi USB: unknown (%s)" message
          CssClass = "unknown" }
