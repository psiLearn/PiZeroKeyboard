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

    let renderSpecialKeys () =
        let specialKeyButton label token extraClass =
            let className =
                if String.IsNullOrWhiteSpace extraClass then
                    "secondary key"
                else
                    sprintf "secondary key %s" extraClass

            button
                [ _type "button"
                  _class className
                  attr "data-token" token ]
                [ str label ]

            div [ _class "special-keys" ] [
                div [ _class "desktop-only" ] [
                div [ _class "key-row function-row" ] [
                    specialKeyButton "F1" "{F1}" ""
                    specialKeyButton "F2" "{F2}" ""
                    specialKeyButton "F3" "{F3}" ""
                    specialKeyButton "F4" "{F4}" ""
                    specialKeyButton "F5" "{F5}" ""
                    specialKeyButton "F6" "{F6}" ""
                    specialKeyButton "F7" "{F7}" ""
                    specialKeyButton "F8" "{F8}" ""
                    specialKeyButton "F9" "{F9}" ""
                    specialKeyButton "F10" "{F10}" ""
                    specialKeyButton "F11" "{F11}" ""
                    specialKeyButton "F12" "{F12}" ""
                ]
                div [ _class "key-row utility-row" ] [
                    specialKeyButton "Print" "{PRINT}" ""
                    specialKeyButton "Scroll" "{SCROLLLOCK}" ""
                    specialKeyButton "Pause" "{PAUSE}" ""
                ]
                div [ _class "key-row nav-row" ] [
                    specialKeyButton "Ins" "{INSERT}" ""
                    specialKeyButton "Home" "{HOME}" ""
                    specialKeyButton "End" "{END}" ""
                    specialKeyButton "PgUp" "{PAGEUP}" ""
                    specialKeyButton "PgDn" "{PAGEDOWN}" ""
                ]
            ]
            div [ _class "keyboard-block" ] [
                div [ _class "key-row keyboard-row" ] [
                    specialKeyButton "Esc" "{ESC}" ""
                    specialKeyButton "Tab" "{TAB}" "wide-2"
                    specialKeyButton "Enter" "{ENTER}" "wide-2"
                    specialKeyButton "Backspace" "{BACKSPACE}" "wide-3"
                    specialKeyButton "Delete" "{DELETE}" "wide-2"
                ]
                div [ _class "key-row keyboard-row" ] [
                    specialKeyButton "Ctrl" "{CTRL}" "wide-2"
                    specialKeyButton "Win" "{WIN}" "wide-2"
                    specialKeyButton "Alt" "{ALT}" "wide-2"
                    specialKeyButton "Space" " " "wide-4"
                    specialKeyButton "Shift" "{SHIFT}" "wide-2"
                ]
            ]
            div [ _class "key-row shortcut-row" ] [
                specialKeyButton "Ctrl+A" "{CTRL+A}" ""
                specialKeyButton "Ctrl+C" "{CTRL+C}" ""
                specialKeyButton "Ctrl+V" "{CTRL+V}" ""
                specialKeyButton "Ctrl+X" "{CTRL+X}" ""
                specialKeyButton "Ctrl+Z" "{CTRL+Z}" ""
            ]
            div [ _class "key-row arrow-block" ] [
                div [ _class "arrow-pad" ] [
                    div [ _class "arrow-row" ] [
                        specialKeyButton "Up" "{UP}" ""
                    ]
                    div [ _class "arrow-row" ] [
                        specialKeyButton "Left" "{LEFT}" ""
                        specialKeyButton "Down" "{DOWN}" ""
                        specialKeyButton "Right" "{RIGHT}" ""
                    ]
                ]
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

    let renderForm (model: IndexViewModel) =
        [ renderLayoutRow model.Layout
          textarea
              [ _id "text"; _name "text"; _placeholder "Paste text here..." ]
              [ str model.Text ]
          button [ _type "submit"; _class "primary send" ] [ str "Send" ]
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
