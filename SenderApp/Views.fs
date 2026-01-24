namespace SenderApp

module Views =
    open System
    open Giraffe
    open Giraffe.ViewEngine

    let renderHead () =
        head [] [
            title [] [ str "LinuxKey Sender" ]
            meta [ _charset "utf-8" ]
            meta [ _name "viewport"; _content "width=device-width, initial-scale=1" ]
            link [ _rel "stylesheet"; _href "/sender.css" ]
            script [ _src "/history.js"; attr "defer" "defer" ] []
            script [ _src "/sender.js"; attr "defer" "defer" ] []
        ]

    let buildStatusNodes showDevInfo (settings: SenderSettings) status =
        match status with
        | Idle -> []
        | Success bytes ->
            if showDevInfo then
                [ div
                      [ _class "alert success" ]
                      [ str (sprintf "Sent %d bytes to %s:%d." bytes settings.TargetIp settings.TargetPort) ] ]
            else
                []
        | Failure message ->
            [ div
                  [ _class "alert error" ]
                  [ str (sprintf "Failed to send text: %s" message) ] ]

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

    type private KeySpec =
        { Label: string
          Token: string
          ExtraClass: string }

    let private specialKeyButton (spec: KeySpec) =
        let className =
            if String.IsNullOrWhiteSpace spec.ExtraClass then
                "secondary key"
            else
                sprintf "secondary key %s" spec.ExtraClass

        button
            [ _type "button"
              _class className
              attr "data-token" spec.Token ]
            [ str spec.Label ]

    let private renderKeyRow rowClass specs =
        div [ _class rowClass ] (specs |> List.map specialKeyButton)

    let private renderDesktopKeys () =
        let functionKeys =
            [ "F1"; "F2"; "F3"; "F4"; "F5"; "F6"; "F7"; "F8"; "F9"; "F10"; "F11"; "F12" ]
            |> List.map (fun label -> { Label = label; Token = sprintf "{%s}" label; ExtraClass = "" })

        let utilityKeys =
            [ { Label = "Print"; Token = "{PRINT}"; ExtraClass = "" }
              { Label = "Scroll"; Token = "{SCROLLLOCK}"; ExtraClass = "" }
              { Label = "Pause"; Token = "{PAUSE}"; ExtraClass = "" } ]

        let navKeys =
            [ { Label = "Ins"; Token = "{INSERT}"; ExtraClass = "" }
              { Label = "Home"; Token = "{HOME}"; ExtraClass = "" }
              { Label = "End"; Token = "{END}"; ExtraClass = "" }
              { Label = "PgUp"; Token = "{PAGEUP}"; ExtraClass = "" }
              { Label = "PgDn"; Token = "{PAGEDOWN}"; ExtraClass = "" } ]

        div [ _class "desktop-only" ] [
            renderKeyRow "key-row function-row" functionKeys
            renderKeyRow "key-row utility-row" utilityKeys
            renderKeyRow "key-row nav-row" navKeys
        ]

    let private renderKeyboardBlock () =
        let row1 =
            [ { Label = "Esc"; Token = "{ESC}"; ExtraClass = "" }
              { Label = "Tab"; Token = "{TAB}"; ExtraClass = "wide-2" }
              { Label = "Enter"; Token = "{ENTER}"; ExtraClass = "wide-2" }
              { Label = "Backspace"; Token = "{BACKSPACE}"; ExtraClass = "wide-3" }
              { Label = "Delete"; Token = "{DELETE}"; ExtraClass = "wide-2" } ]

        let row2 =
            [ { Label = "Ctrl"; Token = "{CTRL}"; ExtraClass = "wide-2" }
              { Label = "Win"; Token = "{WIN}"; ExtraClass = "wide-2" }
              { Label = "Alt"; Token = "{ALT}"; ExtraClass = "wide-2" }
              { Label = "Space"; Token = " "; ExtraClass = "wide-4" }
              { Label = "Shift"; Token = "{SHIFT}"; ExtraClass = "wide-2" } ]

        div [ _class "keyboard-block" ] [
            renderKeyRow "key-row keyboard-row" row1
            renderKeyRow "key-row keyboard-row" row2
        ]

    let private renderShortcutRow () =
        let shortcuts =
            [ "Ctrl+A"; "Ctrl+C"; "Ctrl+V"; "Ctrl+X"; "Ctrl+Z" ]
            |> List.map (fun label -> { Label = label; Token = sprintf "{%s}" label; ExtraClass = "" })
        renderKeyRow "key-row shortcut-row" shortcuts

    let private renderArrowBlock () =
        let upKey = [ { Label = "Up"; Token = "{UP}"; ExtraClass = "" } ]
        let downKeys =
            [ { Label = "Left"; Token = "{LEFT}"; ExtraClass = "" }
              { Label = "Down"; Token = "{DOWN}"; ExtraClass = "" }
              { Label = "Right"; Token = "{RIGHT}"; ExtraClass = "" } ]

        div [ _class "key-row arrow-block" ] [
            div [ _class "arrow-pad" ] [
                renderKeyRow "arrow-row" upKey
                renderKeyRow "arrow-row" downKeys
            ]
        ]

    let renderSpecialKeys () =
        div [ _class "special-keys" ] [
            renderDesktopKeys ()
            renderKeyboardBlock ()
            renderShortcutRow ()
            renderArrowBlock ()
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

    let renderHistoryControls () =
        div [ _class "send-row" ] [
            button [ _type "submit"; _class "primary send"; _id "send-text" ] [ str "Send" ]
            button
                [ _type "button"
                  _id "history-back"
                  _class "secondary history-btn"
                  attr "aria-label" "Previous in history" ]
                [ str "Back" ]
            button
                [ _type "button"
                  _id "history-forward"
                  _class "secondary history-btn"
                  attr "aria-label" "Next in history" ]
                [ str "Forward" ]
        ]

    let renderForm (model: IndexViewModel) =
        [ renderLayoutRow model.Layout
          textarea
              [ _id "text"; _name "text"; _placeholder "Paste text here..." ]
              [ str model.Text ]
          renderHistoryControls ()
          renderSpecialKeys ()
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
              button [ _type "button"; _id "refresh-status"; _class "secondary" ] [ str "Refresh" ]
          ]
          h1 [] [ str "LinuxKey Sender" ] ]
        @ (if showTarget then
               [ p [ _class "target" ] [ str (sprintf "Target device: %s:%d" settings.TargetIp settings.TargetPort) ] ]
           else
               [])

    let renderPage (settings: SenderSettings) (model: IndexViewModel) showDevInfo : HttpHandler =
        let statusNodes = buildStatusNodes showDevInfo settings model.Status
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
