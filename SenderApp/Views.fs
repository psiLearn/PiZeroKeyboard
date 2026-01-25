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
            script [ _src "/history.js"; attr "defer" "defer" ] []
            script [ _src "/sender.js"; attr "defer" "defer" ] []
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
            let percentage = if progress.TotalBytes > 0 then (progress.BytesSent * 100) / progress.TotalBytes else 0
            let remaining = progress.TotalBytes - progress.BytesSent
            [ div [ _class "alert info" ] [
                div [ _class "status-header" ] [
                    str "Sending… "
                    span [ _class "progress-percentage" ] [ str (sprintf "%d%%" percentage) ]
                    span [ _class "progress-detail" ] [ str (sprintf " (%d/%d bytes)" progress.BytesSent progress.TotalBytes) ]
                ]
                div [ _class "progress-bar" ] [
                    div [ _class "progress-fill"; _style (sprintf "width: %d%%" percentage) ] []
                ]
                if remaining > 0 then
                    div [ _class "status-info" ] [
                        str (sprintf "Sending remaining %d bytes…" remaining)
                    ]
                div [ _class "status-actions" ] [
                    button [ _type "button"; _id "abort-send"; _class "secondary"; _title "Abort current send operation" ] [ str "✕ Abort" ]
                ]
            ] ]
        | Success bytes ->
            let duration = calculateSendDuration ()
            let durationStr = if duration > 0 then sprintf " in %d ms" duration else ""
            if showDevInfo then
                [ div [ _class "alert success" ] [
                    span [ _class "success-icon" ] [ str "✓" ]
                    str (sprintf " Sent %d bytes to %s:%d%s" bytes settings.TargetIp settings.TargetPort durationStr)
                ] ]
            else
                [ div [ _class "alert success" ] [
                    span [ _class "success-icon" ] [ str "✓" ]
                    str (sprintf " Text sent successfully! (%d bytes%s)" bytes durationStr)
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
        div [ _class "sending-controls" ] [
            div [ _class "section-header" ] [ str "⏱️ Timing" ]
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
            
            div [ _class "section-header" ] [ str "📦 Reliability" ]
            div [ _class "control-section" ] [
                label [ _for "chunk-size" ] [
                    str "Chunk size (bytes): "
                    span [ _class "control-hint"; _title "Text is sent in chunks of this many bytes. Smaller chunks help with apps that drop input under load." ] [ str "(for apps that drop keys under load)" ]
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
                    span [ _id "chunk-size-value"; _class "control-value" ] [ str "bytes" ]
                ]
                div [ _class "preset-buttons" ] [
                    button [ _type "button"; _class "preset"; attr "data-preset-chunk" "256" ] [ str "256B" ]
                    button [ _type "button"; _class "preset"; attr "data-preset-chunk" "512" ] [ str "512B" ]
                    button [ _type "button"; _class "preset"; attr "data-preset-chunk" "1024" ] [ str "1KB" ]
                    button [ _type "button"; _class "preset"; attr "data-preset-chunk" "2048" ] [ str "2KB" ]
                ]
            ]
            
            div [ _class "section-header" ] [ str "📝 Text Formatting" ]
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
                    str "🔒 Private send (don't save to history)"
                ]
                
                label [ _class "checkbox-label" ] [
                    input [ _type "checkbox"; _id "auto-retry"; _name "auto-retry"; _title "Automatically retry connection every 5 seconds when disconnected" ]
                    str "🔄 Auto-retry (every 5s when disconnected)"
                ]
                
                div [ _id "retry-countdown"; _class "retry-countdown" ] []
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

    let renderKeyboardToggle (visibility: KeyboardVisibility) =
        let (buttonText, isVisible) = 
            match visibility with
            | Visible -> ("Hide Keyboard", true)
            | Hidden -> ("Show Keyboard", false)
        
        div [ _class "keyboard-toggle-container" ] [
            button 
                [ _type "button"
                  _id "keyboard-toggle"
                  _class "secondary"
                  _title (sprintf "%s to make text area larger" buttonText)
                  attr "aria-pressed" (if isVisible then "true" else "false") ]
                [ str buttonText ]
        ]

    let renderKeyboardSection (model: IndexViewModel) =
        let visibilityClass = match model.KeyboardVisibility with
                              | Visible -> "keyboard-visible"
                              | Hidden -> "keyboard-hidden"
        let isConnected = match model.ConnectionStatus with
                          | Connected _ -> true
                          | NotConnected _ -> false
        
        div [ _class (sprintf "keyboard-section %s" visibilityClass); _id "keyboard-section" ] [
            renderSpecialKeys isConnected
        ]

    let renderConnectionBanner (settings: SenderSettings) (connectionStatus: ConnectionStatus) =
        let bannerClass = getConnectionCssClass connectionStatus
        let bannerText = formatConnectionStatus settings connectionStatus
        let targetDisplay = sprintf "%s:%d" settings.TargetIp settings.TargetPort
        
        div [ _class (sprintf "connection-banner %s" bannerClass) ] [
            span [ _class "connection-icon" ] []
            div [ _class "connection-content" ] [
                div [ _class "connection-text" ] [ str bannerText ]
                // Show target info with copy button
                div [ _class "connection-target" ] [ 
                    str "Target: "
                    span [ _id "target-display" ] [ str targetDisplay ]
                    button [ _type "button"; _id "copy-target"; _class "copy-btn"; _title "Copy target address" ] [ str "📋" ]
                ]
                // Collapsible help section for disconnected state
                match connectionStatus with
                | NotConnected info ->
                    details [ _class "connection-details" ] [
                        summary [ _class "connection-details-summary" ] [ str "▶ Details" ]
                        div [ _class "connection-details-content" ] [
                            div [ _class "connection-suggestion" ] [
                                str info.Suggestion
                            ]
                            if Option.isSome info.LastAttempt then
                                div [ _class "connection-meta" ] [
                                    str (formatLastAttempt info.LastAttempt)
                                    if info.RetryCount > 0 then
                                        str (sprintf " | Retries: %d" info.RetryCount)
                                ]
                        ]
                    ]
                | Connected _ -> ()
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
          renderStatusLine ()
          renderHistoryDropdown ()
          renderSendingControls model.SendingControls
          renderKeyboardToggle model.KeyboardVisibility
          renderKeyboardSection model
          renderHint () ]

    let renderHeader (settings: SenderSettings) (model: IndexViewModel) showTarget =
        [ div [ _class "status-panel" ] [
              span [ _id "usb-status"
                     _class (sprintf "usb-dot %s" model.UsbStatus.CssClass)
                     _title model.UsbStatus.Text
                     attr "aria-label" model.UsbStatus.Text ] []
              span [ _id "caps-status"
                     _class (sprintf "caps-dot %s" model.CapsLock.CssClass)
                     _title model.CapsLock.Text
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
        @ (if showTarget then
               [ p [ _class "target" ] [ str (sprintf "Target device: %s:%d" settings.TargetIp settings.TargetPort) ] ]
           else
               [])

    let renderPage (settings: SenderSettings) (model: IndexViewModel) showDevInfo : HttpHandler =
        let statusNodes = buildStatusNodes showDevInfo settings model.Status model.SendStartTime
        let headerNodes = renderHeader settings model showDevInfo
        let connectionBannerNode = renderConnectionBanner settings model.ConnectionStatus
        let formNodes = renderForm model

        html [] [
            renderHead ()
            body (bodyAttrs model.IsMobile)
                ([ connectionBannerNode ]
                 @ headerNodes
                 @ statusNodes
                 @ [ form
                         [ _method "post"; _action "/send" ]
                         formNodes ])
        ]
        |> htmlView
