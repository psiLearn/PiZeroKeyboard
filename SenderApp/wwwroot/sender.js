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
  const form = textarea ? textarea.closest('form') : null;
  const historyBack = document.getElementById('history-back');
  const historyForward = document.getElementById('history-forward');
  const historyKey = 'linuxkey-history';
  const historyIndexKey = 'linuxkey-history-index';
  const historyApi = window.LinuxKeyHistory;
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

  const updateHistoryButtons = (index, items) => {
    if (historyBack) historyBack.disabled = index <= 0;
    if (historyForward) historyForward.disabled = index >= items.length - 1;
  };

  const applyHistory = (index, items) => {
    if (!textarea) return;
    const value = items[index] ?? '';
    textarea.value = value;
    const caret = value.length;
    textarea.setSelectionRange(caret, caret);
    textarea.focus();
  };

  let historyItems = [];
  let historyIndex = 0;

  const loadHistoryState = () => {
    if (!historyApi) return { items: [], index: 0 };
    return historyApi.loadHistoryState(localStorage, historyKey, historyIndexKey);
  };

  const persistHistoryIndex = (index) => {
    if (!historyApi) return;
    historyApi.writeHistoryIndex(localStorage, historyIndexKey, index);
  };

  const addHistoryEntry = (text) => {
    if (!historyApi) return { items: historyItems, index: historyIndex };
    return historyApi.addHistoryEntry(localStorage, historyKey, historyIndexKey, text);
  };

  const refreshHistoryState = () => {
    const state = loadHistoryState();
    historyItems = state.items;
    historyIndex = state.index;
    updateHistoryButtons(historyIndex, historyItems);
  };

  if (historyBack || historyForward) {
    refreshHistoryState();

    if (historyBack) {
      historyBack.addEventListener('click', (event) => {
        event.preventDefault();
        if (historyIndex > 0) {
          historyIndex -= 1;
          persistHistoryIndex(historyIndex);
          applyHistory(historyIndex, historyItems);
          updateHistoryButtons(historyIndex, historyItems);
        }
      });
    }

    if (historyForward) {
      historyForward.addEventListener('click', (event) => {
        event.preventDefault();
        if (historyIndex < historyItems.length - 1) {
          historyIndex += 1;
          persistHistoryIndex(historyIndex);
          applyHistory(historyIndex, historyItems);
          updateHistoryButtons(historyIndex, historyItems);
        }
      });
    }
  }

  if (form && textarea) {
    form.addEventListener('submit', () => {
      const state = addHistoryEntry(textarea.value);
      historyItems = state.items;
      historyIndex = state.index;
      updateHistoryButtons(historyIndex, historyItems);
    });
  }
});
