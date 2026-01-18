namespace SenderApp

module Views =
    open System
    open Giraffe
    open Giraffe.ViewEngine

    let senderStyles =
        """
    @import url('https://fonts.googleapis.com/css2?family=Manrope:wght@400;600;700&family=JetBrains+Mono:wght@400;600&display=swap');
    :root {
      --bg: #f7f4ee;
      --panel: #ffffff;
      --text: #0f172a;
      --muted: #475569;
      --accent: #0f766e;
      --accent-dark: #0b5f58;
      --secondary: #475569;
      --border: #cbd5e1;
      --success: #16a34a;
      --danger: #dc2626;
      --warning: #d97706;
      --unknown: #64748b;
    }
    body { font-family: 'Manrope', 'Segoe UI', sans-serif; margin: 2rem auto; max-width: 48rem; color: var(--text); background: var(--bg); padding: 0 1.5rem; }
    h1 { color: var(--accent); letter-spacing: 0.02em; }
    form { margin-top: 1.5rem; display: flex; flex-direction: column; gap: 1rem; }
    textarea { min-height: 12rem; padding: 1rem; font-size: 1rem; font-family: 'JetBrains Mono', Consolas, monospace; border: 1px solid var(--border); border-radius: 0.5rem; background: var(--panel); color: inherit; }
    button { padding: 0.75rem 1.5rem; border-radius: 0.5rem; border: none; font-size: 1rem; cursor: pointer; }
    button.primary { background: var(--accent); color: white; align-self: flex-start; }
    button.primary:hover { background: var(--accent-dark); }
    button.secondary { background: var(--secondary); color: white; }
    button.secondary:hover { background: #334155; }
    .hint { margin: 0; font-size: 0.85rem; color: #64748b; line-height: 1.4; }
    .hint code { background: #e2e8f0; padding: 0.1rem 0.35rem; border-radius: 0.25rem; font-family: 'JetBrains Mono', Consolas, monospace; }
    .alert { padding: 0.75rem 1rem; border-radius: 0.5rem; border: 1px solid transparent; }
    .alert.success { background: #dcfce7; border-color: #86efac; color: #166534; }
    .alert.error { background: #fee2e2; border-color: #fca5a5; color: #991b1b; }
    .target { font-size: 0.95rem; color: var(--muted); }
    .layout-row { display: flex; align-items: center; gap: 0.75rem; flex-wrap: wrap; }
    .layout-row label { font-weight: 600; }
    .layout-row select { padding: 0.4rem 0.6rem; border-radius: 0.4rem; border: 1px solid var(--border); background: var(--panel); }
    .status-panel { position: fixed; top: 1rem; right: 1rem; display: flex; align-items: center; gap: 0.45rem; background: rgba(255, 255, 255, 0.9); padding: 0.35rem 0.6rem; border-radius: 999px; border: 1px solid var(--border); box-shadow: 0 12px 24px rgba(15, 23, 42, 0.08); z-index: 10; backdrop-filter: blur(6px); }
    .status-panel button { padding: 0.35rem 0.7rem; font-size: 0.8rem; }
    .usb-dot { width: 0.7rem; height: 0.7rem; border-radius: 50%; background: var(--unknown); box-shadow: 0 0 0 3px rgba(100, 116, 139, 0.2); }
    .usb-dot.connected { background: var(--success); box-shadow: 0 0 0 3px rgba(22, 163, 74, 0.2); }
    .usb-dot.disconnected { background: var(--danger); box-shadow: 0 0 0 3px rgba(220, 38, 38, 0.2); }
    .usb-dot.pending { background: var(--warning); box-shadow: 0 0 0 3px rgba(217, 119, 6, 0.2); }
    .usb-dot.unknown { background: var(--unknown); box-shadow: 0 0 0 3px rgba(100, 116, 139, 0.2); }
    .special-keys { display: flex; flex-direction: column; gap: 0.6rem; }
    .desktop-only { display: flex; flex-direction: column; gap: 0.5rem; }
    .mobile .desktop-only { display: none; }
    .key-row { display: flex; gap: 0.5rem; flex-wrap: wrap; align-items: center; }
    .special-keys button { padding: 0.35rem 0.6rem; font-size: 0.85rem; }
    .arrow-pad { display: flex; flex-direction: column; align-items: center; gap: 0.35rem; }
    .arrow-row { display: flex; gap: 0.4rem; justify-content: center; }
    .send { margin-top: 0.1rem; }
    body.mobile { margin: 1rem auto; padding: 0 1rem; }
    body.mobile h1 { font-size: 1.6rem; }
    body.mobile textarea { min-height: 10rem; font-size: 1rem; }
    body.mobile button.primary { width: 100%; align-self: stretch; }
    @media (max-width: 720px) {
      body { margin: 1rem auto; padding: 0 1rem; }
      h1 { font-size: 1.6rem; }
      textarea { min-height: 10rem; font-size: 1rem; }
      button.primary { width: 100%; align-self: stretch; }
      .status-panel { top: 0.6rem; right: 0.6rem; }
      .desktop-only { display: none; }
    }
    """

    let senderScript =
        """
    document.addEventListener('DOMContentLoaded', () => {
      const statusEl = document.getElementById('usb-status');
      const refreshBtn = document.getElementById('refresh-status');

      const setStatus = (text, cssClass) => {
        if (!statusEl) return;
        statusEl.className = `usb-dot ${cssClass || 'unknown'}`;
        statusEl.setAttribute('title', text);
        statusEl.setAttribute('aria-label', text);
      };

      const refreshStatus = () => {
        if (!statusEl) return;
        if (refreshBtn) refreshBtn.disabled = true;
        fetch('/status', { cache: 'no-store' })
          .then((resp) => resp.ok ? resp.json() : Promise.reject(resp.status))
          .then((data) => setStatus(data.text || 'Raspberry Pi USB: unknown', data.cssClass || 'unknown'))
          .catch(() => setStatus('Raspberry Pi USB: unknown (refresh failed)', 'unknown'))
          .finally(() => { if (refreshBtn) refreshBtn.disabled = false; });
      };

      if (refreshBtn) {
        refreshBtn.addEventListener('click', (event) => {
          event.preventDefault();
          refreshStatus();
        });
      }

      const connectWebSocket = () => {
        if (!('WebSocket' in window)) {
          return;
        }
        const scheme = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
        const url = `${scheme}//${window.location.host}/status/ws`;
        const socket = new WebSocket(url);
        socket.onmessage = (event) => {
          try {
            const data = JSON.parse(event.data);
            setStatus(data.text || 'Raspberry Pi USB: unknown', data.cssClass || 'unknown');
          } catch {
            setStatus('Raspberry Pi USB: unknown', 'unknown');
          }
        };
        socket.onerror = () => {
          socket.close();
        };
        socket.onclose = () => {
          window.setTimeout(connectWebSocket, 3000);
        };
      };

      connectWebSocket();

      const textarea = document.getElementById('text');
      const insertToken = (token) => {
        if (!textarea || !token) return;
        const start = textarea.selectionStart ?? textarea.value.length;
        const end = textarea.selectionEnd ?? textarea.value.length;
        textarea.value = `${textarea.value.slice(0, start)}${token}${textarea.value.slice(end)}`;
        const caret = start + token.length;
        textarea.setSelectionRange(caret, caret);
        textarea.focus();
      };

      document.querySelectorAll('[data-token]').forEach((button) => {
        button.addEventListener('click', (event) => {
          event.preventDefault();
          insertToken(button.getAttribute('data-token'));
        });
      });
    });
    """

    let renderHead () =
        head [] [
            title [] [ str "LinuxKey Sender" ]
            meta [ _charset "utf-8" ]
            meta [ _name "viewport"; _content "width=device-width, initial-scale=1" ]
            style [] [ rawText senderStyles ]
            script [] [ rawText senderScript ]
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
        let specialKeyButton label token =
            button
                [ _type "button"
                  _class "secondary"
                  attr "data-token" token ]
                [ str label ]

        div [ _class "special-keys" ] [
            div [ _class "desktop-only" ] [
                div [ _class "key-row" ] [
                    specialKeyButton "F1" "{F1}"
                    specialKeyButton "F2" "{F2}"
                    specialKeyButton "F3" "{F3}"
                    specialKeyButton "F4" "{F4}"
                    specialKeyButton "F5" "{F5}"
                    specialKeyButton "F6" "{F6}"
                    specialKeyButton "F7" "{F7}"
                    specialKeyButton "F8" "{F8}"
                    specialKeyButton "F9" "{F9}"
                    specialKeyButton "F10" "{F10}"
                    specialKeyButton "F11" "{F11}"
                    specialKeyButton "F12" "{F12}"
                ]
                div [ _class "key-row" ] [
                    specialKeyButton "Print" "{PRINT}"
                    specialKeyButton "Scroll" "{SCROLLLOCK}"
                    specialKeyButton "Pause" "{PAUSE}"
                ]
                div [ _class "key-row" ] [
                    specialKeyButton "Ins" "{INSERT}"
                    specialKeyButton "Home" "{HOME}"
                    specialKeyButton "End" "{END}"
                    specialKeyButton "PgUp" "{PAGEUP}"
                    specialKeyButton "PgDn" "{PAGEDOWN}"
                ]
            ]
            div [ _class "key-row" ] [
                specialKeyButton "Esc" "{ESC}"
                specialKeyButton "Tab" "{TAB}"
                specialKeyButton "Enter" "{ENTER}"
                specialKeyButton "Backspace" "{BACKSPACE}"
                specialKeyButton "Delete" "{DELETE}"
            ]
            div [ _class "key-row" ] [
                specialKeyButton "Ctrl+A" "{CTRL+A}"
                specialKeyButton "Ctrl+C" "{CTRL+C}"
                specialKeyButton "Ctrl+V" "{CTRL+V}"
                specialKeyButton "Ctrl+X" "{CTRL+X}"
                specialKeyButton "Ctrl+Z" "{CTRL+Z}"
                specialKeyButton "Win" "{WIN}"
            ]
            div [ _class "key-row" ] [
                div [ _class "arrow-pad" ] [
                    div [ _class "arrow-row" ] [
                        specialKeyButton "Up" "{UP}"
                    ]
                    div [ _class "arrow-row" ] [
                        specialKeyButton "Left" "{LEFT}"
                        specialKeyButton "Down" "{DOWN}"
                        specialKeyButton "Right" "{RIGHT}"
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
