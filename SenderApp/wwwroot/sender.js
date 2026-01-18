document.addEventListener('DOMContentLoaded', () => {
  const statusEl = document.getElementById('usb-status');
  const capsEl = document.getElementById('caps-status');
  const refreshBtn = document.getElementById('refresh-status');

  const setDot = (element, baseClass, text, cssClass) => {
    if (!element) return;
    element.className = `${baseClass} ${cssClass || 'unknown'}`;
    element.setAttribute('title', text);
    element.setAttribute('aria-label', text);
  };

  const applyStatus = (data) => {
    setDot(statusEl, 'usb-dot', data.text || 'Raspberry Pi USB: unknown', data.cssClass || 'unknown');
    setDot(capsEl, 'caps-dot', data.capsText || 'Caps Lock: unknown', data.capsCssClass || 'unknown');
  };

  const refreshStatus = () => {
    if (!statusEl) return;
    if (refreshBtn) refreshBtn.disabled = true;
    fetch('/status', { cache: 'no-store' })
      .then((resp) => resp.ok ? resp.json() : Promise.reject(resp.status))
      .then((data) => applyStatus(data))
      .catch(() => applyStatus({ text: 'Raspberry Pi USB: unknown (refresh failed)', cssClass: 'unknown' }))
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
        applyStatus(data);
      } catch {
        applyStatus({ text: 'Raspberry Pi USB: unknown', cssClass: 'unknown' });
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
