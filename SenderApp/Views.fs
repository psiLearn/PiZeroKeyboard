namespace SenderApp

module Views =
    open System
    open Giraffe
    open Giraffe.ViewEngine
    open SenderApp.ConnectionService
    open SenderApp.ConnectionRetryService
    open SenderApp.ModifierKeyService
    open SenderApp.SendingControlsService

    let renderHead () =
        head [] [
            title [] [ str "LinuxKey Sender" ]
            meta [ _charset "utf-8" ]
            meta [ _name "viewport"; _content "width=device-width, initial-scale=1" ]
            link [ _rel "stylesheet"; _href "https://fonts.googleapis.com/css2?family=Material+Symbols+Rounded:opsz,wght,FILL,GRAD@48,400,0,0" ]
            link [ _rel "stylesheet"; _href "/sender.css" ]
            script [ _src "/src/Sender.js"; attr "type" "module" ] []
        ]

    let buildStatusNodes showDevInfo (settings: SenderSettings) status sendStartTime =
        let calculateSendDuration () : int =
            match sendStartTime with
            | Some startTime ->
                let duration : TimeSpan = DateTime.UtcNow - startTime
                int (duration.TotalMilliseconds)
            | None -> 0
            
        match status with
        | Idle -> []
        | Sending progress ->
            let percentage = if progress.TotalCharacters > 0 then (progress.CharactersSent * 100) / progress.TotalCharacters else 0
            let remaining = progress.TotalCharacters - progress.CharactersSent
            [ div [ _class "alert info" ] [
                div [ _class "status-header" ] [
                    str "Sending… "
                    span [ _class "progress-percentage" ] [ str (sprintf "%d%%" percentage) ]
                    span [ _class "progress-detail" ] [ str (sprintf " (%d/%d chars)" progress.CharactersSent progress.TotalCharacters) ]
                ]
                div [ _class "progress-bar" ] [
                    div [ _class "progress-fill"; _style (sprintf "width: %d%%" percentage) ] []
                ]
                if remaining > 0 then
                    div [ _class "status-info" ] [
                        str (sprintf "Sending remaining %d chars…" remaining)
                    ]
                div [ _class "status-actions" ] [
                    button [ _type "button"; _id "abort-send"; _class "secondary"; _title "Abort current send operation" ] [ str "✕ Abort" ]
                ]
            ] ]
        | Success charCount ->
            let duration = calculateSendDuration ()
            let durationStr = if duration > 0 then sprintf " in %d ms" duration else ""
            if showDevInfo then
                [ div [ _class "alert success" ] [
                    span [ _class "success-icon" ] [ str "✓" ]
                    str (sprintf " Sent %d chars to %s:%d%s" charCount settings.TargetIp settings.TargetPort durationStr)
                ] ]
            else
                [ div [ _class "alert success" ] [
                    span [ _class "success-icon" ] [ str "✓" ]
                    str (sprintf " Text sent successfully! (%d chars%s)" charCount durationStr)
                ] ]
        | Failure message ->
            [ div [ _class "alert error" ] [
                span [ _class "error-icon" ] [ str "✕" ]
                str (sprintf " Failed to send text: %s" message)
            ] ]

    let bodyAttrs isMobile =
        if isMobile then
            [ _class "mobile" ]
        else
            []

    let layoutOptionAttrs selectedLayout value =
        if String.Equals(selectedLayout, value, StringComparison.OrdinalIgnoreCase) then
            [ _value value; attr "selected" "selected" ]
        else
            [ _value value ]

    let renderLayoutRow selectedLayout =
        div
            [ _class "layout-row" ]
            [ label [ _for "layout" ] [ str "Keyboard layout" ]
              select [ _id "layout"; _name "layout" ] [
                  option (layoutOptionAttrs selectedLayout "en") [ str "English (US)" ]
                  option (layoutOptionAttrs selectedLayout "de") [ str "Deutsch (DE)" ]
              ] ]

    let renderSendingControls (controls: SendingControls) =
        details [ _class "settings-panel" ] [
            summary [ _class "settings-summary" ] [
                span [ _class "material-symbols-rounded sm" ] [ str "settings" ]
                str "Settings"
            ]
            div [ _class "sending-controls" ] [
                div [ _class "section-header" ] [
                    span [ _class "material-symbols-rounded sm" ] [ str "timer" ]
                    str "Timing"
                ]
                div [ _class "control-section" ] [
                    label [ _for "typing-delay" ] [ 
                        str "Typing delay (ms per character): "
                        span [ _class "control-hint" ] [ str "(0 for instant)" ]
                    ]
                    div [ _class "control-row" ] [
                        input [ 
                            _type "range"
                            _id "typing-delay"
                            _name "typing-delay"
                            _min "0"
                            _max "1000"
                            _value (string controls.TypingDelayMs)
                            _class "control-slider"
                        ]
                        input [
                            _type "number"
                            _id "typing-delay-input"
                            _min "0"
                            _max "1000"
                            _value (string controls.TypingDelayMs)
                            _class "control-number-input"
                        ]
                        span [ _id "typing-delay-value"; _class "control-value" ] [ str "ms" ]
                    ]
                    div [ _class "preset-buttons" ] [
                        button [ _type "button"; _class "preset"; attr "data-preset-delay" "0" ] [ str "Instant" ]
                        button [ _type "button"; _class "preset"; attr "data-preset-delay" "2" ] [ str "2ms" ]
                        button [ _type "button"; _class "preset"; attr "data-preset-delay" "5" ] [ str "5ms" ]
                        button [ _type "button"; _class "preset"; attr "data-preset-delay" "10" ] [ str "10ms" ]
                    ]
                ]
                
                div [ _class "section-header" ] [
                    span [ _class "material-symbols-rounded sm" ] [ str "inventory_2" ]
                    str "Reliability"
                ]
                div [ _class "control-section" ] [
                    label [ _for "chunk-size" ] [
                        str "Chunk size (chars): "
                        span [ _class "control-hint"; _title "Text is sent in chunks of this many characters. Smaller chunks help with apps that drop input under load." ] [ str "(for apps that drop keys under load)" ]
                    ]
                    div [ _class "control-row" ] [
                        input [
                            _type "range"
                            _id "chunk-size"
                            _name "chunk-size"
                            _min "1"
                            _max "10000"
                            _value (string controls.ChunkSize)
                            _class "control-slider"
                        ]
                        input [
                            _type "number"
                            _id "chunk-size-input"
                            _min "1"
                            _max "10000"
                            _value (string controls.ChunkSize)
                            _class "control-number-input"
                        ]
                        span [ _id "chunk-size-value"; _class "control-value" ] [ str "chars" ]
                    ]
                    div [ _class "preset-buttons" ] [
                        button [ _type "button"; _class "preset"; attr "data-preset-chunk" "256" ] [ str "256c" ]
                        button [ _type "button"; _class "preset"; attr "data-preset-chunk" "512" ] [ str "512c" ]
                        button [ _type "button"; _class "preset"; attr "data-preset-chunk" "1024" ] [ str "1Kc" ]
                        button [ _type "button"; _class "preset"; attr "data-preset-chunk" "2048" ] [ str "2Kc" ]
                    ]
                ]
                
                div [ _class "section-header" ] [
                    span [ _class "material-symbols-rounded sm" ] [ str "edit_note" ]
                    str "Text Formatting"
                ]
                div [ _class "control-section checkbox-group" ] [
                    label [ _class "checkbox-label" ] [
                        input [ _type "checkbox"; _id "append-newline"; _name "append-newline"; if controls.AppendNewlineAtEnd then attr "checked" "checked" ]
                        str "Append newline at end"
                    ]
                    
                    label [ _class "checkbox-label" ] [
                        input [ _type "checkbox"; _id "normalize-endings"; _name "normalize-endings"; if controls.NormalizeLineEndings then attr "checked" "checked" ]
                        str "Normalize line endings (CRLF → LF)"
                    ]
                    
                    div [ _class "section-divider" ] []
                    
                    label [ _class "checkbox-label" ] [
                        input [ _type "checkbox"; _id "private-send"; _name "private-send" ]
                        span [ _class "material-symbols-rounded sm" ] [ str "lock" ]
                        str "Private send (don't save to history)"
                    ]
                    
                    label [ _class "checkbox-label" ] [
                        input [ _type "checkbox"; _id "auto-retry"; _name "auto-retry"; _title "Automatically retry connection every 5 seconds when disconnected" ]
                        span [ _class "material-symbols-rounded sm" ] [ str "sync" ]
                        str "Auto-retry (every 5s when disconnected)"
                    ]
                    
                    div [ _id "retry-countdown"; _class "retry-countdown" ] []
                ]
            ]
        ]

    type private KeySpec =
        { Label: string
          Token: string
          ExtraClass: string }

    type private ModifierKeySpec =
        { Label: string
          Token: string
          ExtraClass: string
          IsModifier: bool }

    let private buildKeyClassName baseClass extraClass =
        if String.IsNullOrWhiteSpace extraClass then baseClass
        else sprintf "%s %s" baseClass extraClass

    let private specialKeyButton (spec: KeySpec) =
        button
            [ _type "button"
              _class (buildKeyClassName "secondary key" spec.ExtraClass)
              attr "data-token" spec.Token ]
            [ str spec.Label ]

    let private specialKeyButtonDisabled (spec: KeySpec) =
        button
            [ _type "button"
              _class (buildKeyClassName "secondary key" spec.ExtraClass)
              attr "data-token" spec.Token
              attr "disabled" "disabled" ]
            [ str spec.Label ]

    /// Render a modifier key button with latch/toggle support
    let private modifierKeyButton (spec: ModifierKeySpec) =
        button
            [ _type "button"
              _class (buildKeyClassName "secondary key modifier-key" spec.ExtraClass)
              attr "data-token" spec.Token
              attr "data-modifier" "true"
              attr "aria-pressed" "false"
              _title (sprintf "Click to toggle, hold for momentary (%s)" spec.Label) ]
            [ str spec.Label ]

    let private modifierKeyButtonDisabled (spec: ModifierKeySpec) =
        button
            [ _type "button"
              _class (buildKeyClassName "secondary key modifier-key" spec.ExtraClass)
              attr "data-token" spec.Token
              attr "data-modifier" "true"
              attr "aria-pressed" "false"
              attr "disabled" "disabled"
              _title (sprintf "Click to toggle, hold for momentary (%s)" spec.Label) ]
            [ str spec.Label ]

    let private renderKeyRow rowClass specs =
        div [ _class rowClass ] (specs |> List.map specialKeyButton)

    let private renderKeyRowDisabled rowClass specs =
        div [ _class rowClass ] (specs |> List.map specialKeyButtonDisabled)

    let renderSpecialKeys isConnected =
        div [ _class "special-keys" ] [
            // Function Keys Row
            div [ _class "key-group function-keys-group" ] [
                div [ _class "key-group-label" ] [ str "Function Keys" ]
                let functionKeys =
                    [ "F1"; "F2"; "F3"; "F4"; "F5"; "F6"; "F7"; "F8"; "F9"; "F10"; "F11"; "F12" ]
                    |> List.map (fun label -> { Label = label; Token = sprintf "{%s}" label; ExtraClass = "" })
                if isConnected then
                    renderKeyRow "key-row function-row" functionKeys
                else
                    renderKeyRowDisabled "key-row function-row" functionKeys
            ]
            
            // Navigation & Utility Keys
            div [ _class "key-group navigation-group" ] [
                div [ _class "key-group-label" ] [ str "Navigation & Utilities" ]
                div [ _class "nav-cluster" ] [
                    // Top row: Print, Scroll, Pause
                    let utilityKeys =
                        [ { Label = "Print"; Token = "{PRINT}"; ExtraClass = "" }
                          { Label = "Scroll"; Token = "{SCROLLLOCK}"; ExtraClass = "" }
                          { Label = "Pause"; Token = "{PAUSE}"; ExtraClass = "" } ]
                    if isConnected then
                        renderKeyRow "key-row utility-row" utilityKeys
                    else
                        renderKeyRowDisabled "key-row utility-row" utilityKeys
                    
                    // Bottom row: Insert, Home, End, PgUp, PgDn
                    div [ _class "key-row nav-row" ] [
                        let navKeys =
                            [ { Label = "Ins"; Token = "{INSERT}"; ExtraClass = "" }
                              { Label = "Home"; Token = "{HOME}"; ExtraClass = "" }
                              { Label = "End"; Token = "{END}"; ExtraClass = "" }
                              { Label = "PgUp"; Token = "{PAGEUP}"; ExtraClass = "" }
                              { Label = "PgDn"; Token = "{PAGEDOWN}"; ExtraClass = "" } ]
                        if isConnected then
                            yield! navKeys |> List.map specialKeyButton
                        else
                            yield! navKeys |> List.map specialKeyButtonDisabled
                    ]
                ]
            ]
            
            // Main Keyboard Block
            div [ _class "key-group editing-group" ] [
                div [ _class "key-group-label" ] [ str "Editing Keys" ]
                let row1 =
                    [ { Label = "Esc"; Token = "{ESC}"; ExtraClass = "" }
                      { Label = "Tab"; Token = "{TAB}"; ExtraClass = "wide-2" }
                      { Label = "Enter"; Token = "{ENTER}"; ExtraClass = "wide-2" }
                      { Label = "Backspace"; Token = "{BACKSPACE}"; ExtraClass = "wide-3" }
                      { Label = "Delete"; Token = "{DELETE}"; ExtraClass = "wide-2" } ]
                if isConnected then
                    renderKeyRow "key-row keyboard-row" row1
                else
                    renderKeyRowDisabled "key-row keyboard-row" row1
            ]
            
            // Modifier Keys Row
            div [ _class "key-group modifiers-group" ] [
                div [ _class "key-group-label" ] [ str "Modifiers (click to latch/toggle)" ]
                let row2Modifiers =
                    [ { Label = "Ctrl"; Token = "{CTRL}"; ExtraClass = "wide-2"; IsModifier = true }
                      { Label = "Win"; Token = "{WIN}"; ExtraClass = "wide-2"; IsModifier = true }
                      { Label = "Alt"; Token = "{ALT}"; ExtraClass = "wide-2"; IsModifier = true }
                      { Label = "Space"; Token = " "; ExtraClass = "wide-4"; IsModifier = false }
                      { Label = "Shift"; Token = "{SHIFT}"; ExtraClass = "wide-2"; IsModifier = true } ]
                div [ _class "key-row keyboard-row" ] 
                    (row2Modifiers |> List.map (fun spec ->
                        if spec.IsModifier then
                            if isConnected then
                                modifierKeyButton { Label = spec.Label; Token = spec.Token; ExtraClass = spec.ExtraClass; IsModifier = true }
                            else
                                modifierKeyButtonDisabled { Label = spec.Label; Token = spec.Token; ExtraClass = spec.ExtraClass; IsModifier = true }
                        else
                            if isConnected then
                                specialKeyButton { Label = spec.Label; Token = spec.Token; ExtraClass = spec.ExtraClass }
                            else
                                specialKeyButtonDisabled { Label = spec.Label; Token = spec.Token; ExtraClass = spec.ExtraClass }))
            ]
            
            // Arrows & Common Shortcuts
            div [ _class "key-group arrows-group" ] [
                div [ _class "key-group-label" ] [ str "Navigation & Shortcuts" ]
                div [ _class "arrow-cluster" ] [
                    // Arrow keys
                    let upKey = [ { Label = "Up"; Token = "{UP}"; ExtraClass = "" } ]
                    let downKeys =
                        [ { Label = "Left"; Token = "{LEFT}"; ExtraClass = "" }
                          { Label = "Down"; Token = "{DOWN}"; ExtraClass = "" }
                          { Label = "Right"; Token = "{RIGHT}"; ExtraClass = "" } ]
                    
                    if isConnected then
                        renderKeyRow "arrow-row" upKey
                        renderKeyRow "arrow-row" downKeys
                    else
                        renderKeyRowDisabled "arrow-row" upKey
                        renderKeyRowDisabled "arrow-row" downKeys
                ]
                
                // Common shortcuts
                let shortcuts =
                    [ "Ctrl+A"; "Ctrl+C"; "Ctrl+V"; "Ctrl+X"; "Ctrl+Z" ]
                    |> List.map (fun label -> { Label = label; Token = sprintf "{%s}" label; ExtraClass = "" })
                if isConnected then
                    renderKeyRow "key-row shortcut-row" shortcuts
                else
                    renderKeyRowDisabled "key-row shortcut-row" shortcuts
            ]
        ]

    let renderHint () =
        p [ _class "hint" ]
            [ str "Special keys: "
              code [] [ str "{BACKSPACE}" ]
              str " "
              code [] [ str "{ENTER}" ]
              str " "
              code [] [ str "{TAB}" ]
              str " "
              code [] [ str "{WIN}" ]
              str ". Use "
              code [] [ str "{{" ]
              str " and "
              code [] [ str "}}" ]
              str " for literal braces." ]

    let renderHistoryControls isConnected =
        div [ _class "send-row" ] [
            if isConnected then
                div [ _class "send-button-group" ] [
                    button [ _type "submit"; _class "primary send"; _id "send-text" ] [
                        span [ _class "material-symbols-rounded sm" ] [ str "send" ]
                        str "Send"
                    ]
                    span [ _class "keyboard-hint" ] [ str "Ctrl+Enter" ]
                ]
            else
                div [ _class "send-button-group" ] [
                    button [ _type "submit"; _class "primary send"; _id "send-text"; attr "disabled" "disabled"; _title "Connect to receiver to send" ] [
                        span [ _class "material-symbols-rounded sm" ] [ str "send" ]
                        str "Send"
                    ]
                    span [ _class "keyboard-hint disabled" ] [ str "Ctrl+Enter" ]
                ]
            button [ _type "button"; _class "secondary secondary-action"; _id "clear-text" ] [ str "Clear" ]
            button
                [ _type "button"
                  _id "history-prev"
                  _class "secondary secondary-action history-btn"
                  _title "Load previous snippet from history"
                  attr "aria-label" "Previous snippet in history" ]
                [ str "← Previous" ]
            button
                [ _type "button"
                  _id "history-next"
                  _class "secondary secondary-action history-btn"
                  _title "Load next snippet from history"
                  attr "aria-label" "Next snippet in history" ]
                [ str "Next →" ]
            button [ _type "button"; _class "secondary secondary-action"; _id "history-toggle"; _title "Show/hide history" ] [ str "History" ]
        ]

    let renderStatusLine () =
        div [ _id "status-line"; _class "status-line" ] [ str "Ready" ]

    let renderHistoryDropdown () =
        div [ _id "history-list"; _class "history-dropdown hidden" ] []

    let renderKeyboardPanel (model: IndexViewModel) =
        let isConnected = match model.ConnectionStatus with
                          | Connected _ -> true
                          | NotConnected _ -> false
        let isVisible =
            match model.KeyboardVisibility with
            | Visible -> true
            | Hidden -> false
        details [ _class "keyboard-panel"; if isVisible then attr "open" "open" ] [
            summary [ _class "keyboard-summary" ] [
                span [ _class "material-symbols-rounded sm" ] [ str "keyboard" ]
                str "Keyboard"
            ]
            div [ _class "keyboard-section"; _id "keyboard-section" ] [
                renderSpecialKeys isConnected
            ]
        ]

    let renderForm (model: IndexViewModel) =
        let isConnected = match model.ConnectionStatus with
                          | Connected _ -> true
                          | NotConnected _ -> false
        [ renderLayoutRow model.Layout
          textarea
              [ _id "text"; _name "text"; _placeholder "Paste text here..." ]
              [ str model.Text ]
          renderHistoryControls isConnected
          renderKeyboardPanel model
          renderStatusLine ()
          renderHistoryDropdown ()
          renderSendingControls model.SendingControls
          renderHint () ]

    let renderHeader (settings: SenderSettings) (model: IndexViewModel) _showTarget =
        let connectionTooltip =
            let targetLine = sprintf "Target: %s:%d" settings.TargetIp settings.TargetPort
            let statusLine = formatConnectionStatus settings model.ConnectionStatus
            let extraLines =
                match model.ConnectionStatus with
                | Connected _ -> []
                | NotConnected info ->
                    let lastAttemptLine = formatLastAttempt info.LastAttempt
                    let retryLine = if info.RetryCount > 0 then sprintf "Retries: %d" info.RetryCount else ""
                    [ info.Suggestion; lastAttemptLine; retryLine ]
                    |> List.filter (String.IsNullOrWhiteSpace >> not)
            String.concat "\n" (statusLine :: targetLine :: extraLines)
        [ div [ _class "status-panel"; attr "data-tooltip" connectionTooltip; attr "aria-label" connectionTooltip ] [
              span [ _id "usb-status"
                     _class (sprintf "usb-dot %s" model.UsbStatus.CssClass)
                     attr "aria-label" model.UsbStatus.Text ] []
              span [ _id "caps-status"
                     _class (sprintf "caps-dot %s" model.CapsLock.CssClass)
                     attr "aria-label" model.CapsLock.Text ] []
          ]
          h1 [] [ 
              str "LinuxKey Sender"
              br []
              small [ _class "app-subtitle" ] [ str "Send text/keys to a Linux receiver" ]
          ]
          // Show retry status if retrying
          if model.RetryState.IsRetrying then
              div [ _class "retry-status" ] [
                  str (ConnectionRetryService.formatRetryStatus model.RetryState)
              ]
        ]

    let renderPage (settings: SenderSettings) (model: IndexViewModel) showDevInfo : HttpHandler =
        let statusNodes = buildStatusNodes showDevInfo settings model.Status model.SendStartTime
        let headerNodes = renderHeader settings model showDevInfo
        let formNodes = renderForm model

        html [] [
            renderHead ()
            body (bodyAttrs model.IsMobile)
                (headerNodes
                 @ statusNodes
                 @ [ form
                         [ _method "post"; _action "/send" ]
                         formNodes ])
        ]
        |> htmlView
