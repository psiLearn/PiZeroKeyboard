const handleCopyTarget = () => {
  const copyTargetBtn = document.getElementById('copy-target');
  if (!copyTargetBtn) return;

  copyTargetBtn.addEventListener('click', (event) => {
    event.preventDefault();
    const targetDisplay = document.getElementById('target-display');
    if (!targetDisplay) return;

    const text = targetDisplay.textContent;
    navigator.clipboard.writeText(text)
      .then(() => {
        const originalText = copyTargetBtn.textContent;
        copyTargetBtn.textContent = '✓';
        setTimeout(() => { copyTargetBtn.textContent = originalText; }, 1500);
      })
      .catch(err => console.error('Failed to copy:', err));
  });
};

document.addEventListener('DOMContentLoaded', () => {
  handleCopyTarget();

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
      .then((resp) => resp.ok ? resp.json() : Promise.reject(new Error(`HTTP ${resp.status}`)))
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

  // Auto-retry state
  let autoRetryEnabled = false;
  let autoRetryTimer = null;
  let retryCountdownTimer = null;
  let nextRetryCountdown = 0;

  const updateRetryCountdown = () => {
    const retryCountdownEl = document.getElementById('retry-countdown');
    if (retryCountdownEl && nextRetryCountdown > 0) {
      retryCountdownEl.textContent = `Retrying in ${nextRetryCountdown}s…`;
    } else if (retryCountdownEl) {
      retryCountdownEl.textContent = '';
    }
  };

  const startRetryCountdown = (seconds) => {
    nextRetryCountdown = seconds;
    if (retryCountdownTimer) clearInterval(retryCountdownTimer);
    
    updateRetryCountdown();
    retryCountdownTimer = setInterval(() => {
      nextRetryCountdown -= 1;
      updateRetryCountdown();
      if (nextRetryCountdown <= 0) {
        clearInterval(retryCountdownTimer);
        retryCountdownTimer = null;
      }
    }, 1000);
  };

  const handleAutoRetryCheck = () => {
    const autoRetryCheckbox = document.getElementById('auto-retry');
    if (!autoRetryCheckbox) return;
    
    autoRetryEnabled = autoRetryCheckbox.checked;
    
    if (autoRetryEnabled) {
      // Start auto-retry polling
      if (autoRetryTimer) clearInterval(autoRetryTimer);
      autoRetryTimer = setInterval(() => {
        const statusEl = document.getElementById('usb-status');
        const isConnected = statusEl && statusEl.classList.contains('connected');
        
        if (!isConnected) {
          refreshStatus();
          startRetryCountdown(5);
        }
      }, 5000);
    } else {
      // Stop auto-retry polling
      if (autoRetryTimer) clearInterval(autoRetryTimer);
      autoRetryTimer = null;
      if (retryCountdownTimer) clearInterval(retryCountdownTimer);
      retryCountdownTimer = null;
      nextRetryCountdown = 0;
      updateRetryCountdown();
    }
  };

  const autoRetryCheckbox = document.getElementById('auto-retry');
  if (autoRetryCheckbox) {
    autoRetryCheckbox.addEventListener('change', handleAutoRetryCheck);
  }

  const setupWebSocketConnection = () => {
    if (!('WebSocket' in globalThis)) return;

    const scheme = globalThis.location.protocol === 'https:' ? 'wss:' : 'ws:';
    const url = `${scheme}//${globalThis.location.host}/status/ws`;
    const socket = new WebSocket(url);

    socket.onmessage = (event) => {
      try {
        applyStatus(JSON.parse(event.data));
      } catch {
        applyStatus({ text: 'Raspberry Pi USB: unknown', cssClass: 'unknown' });
      }
    };

    socket.onerror = () => socket.close();
    socket.onclose = () => globalThis.setTimeout(setupWebSocketConnection, 3000);
  };

  setupWebSocketConnection();

  const textarea = document.getElementById('text');
  const form = textarea ? textarea.closest('form') : null;
  const historyBack = document.getElementById('history-back');
  const historyForward = document.getElementById('history-forward');
  const historyKey = 'linuxkey-history';
  const historyIndexKey = 'linuxkey-history-index';
  const historyApi = globalThis.LinuxKeyHistory;

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
      insertToken(button.dataset.token);
    });
  });

  const updateHistoryButtons = (index, items) => {
    if (historyBack) historyBack.disabled = index <= 0;
    if (historyForward) historyForward.disabled = index >= items.length - 1;
  };

  const applyHistory = (index, items) => {
    if (!textarea) return;
    const item = items[index];
    // Support both old string format and new object format
    const value = (typeof item === 'string') ? item : (item?.text ?? '');
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
    // Handle Ctrl+Enter keyboard shortcut to submit form
    textarea.addEventListener('keydown', (event) => {
      if ((event.ctrlKey || event.metaKey) && event.key === 'Enter') {
        event.preventDefault();
        const sendBtn = document.getElementById('send-text');
        if (sendBtn && !sendBtn.disabled) {
          form.submit();
        }
      }
    });

    form.addEventListener('submit', () => {
      const privateSendCheckbox = document.getElementById('private-send');
      const isPrivateSend = privateSendCheckbox?.checked;
      
      if (!isPrivateSend) {
        const state = addHistoryEntry(textarea.value);
        historyItems = state.items;
        historyIndex = state.index;
        updateHistoryButtons(historyIndex, historyItems);
      }
    });
  }

  // Handle history toggle button
  const historyToggle = document.getElementById('history-toggle');
  const historyList = document.getElementById('history-list');
  if (historyToggle && historyList) {
    historyToggle.addEventListener('click', (event) => {
      event.preventDefault();
      historyList.classList.toggle('hidden');
    });
  }

  // Update status line on form submit
  const statusLine = document.getElementById('status-line');
  if (form && statusLine) {
    form.addEventListener('submit', () => {
      statusLine.textContent = 'Sending...';
      statusLine.classList.add('sending');
      statusLine.classList.remove('sent');
    });
  }

  // Simulate success state after send (in real app, this comes from server response)
  const setSentState = () => {
    if (statusLine) {
      statusLine.textContent = 'Sent ✓';
      statusLine.classList.remove('sending');
      statusLine.classList.add('sent');
      setTimeout(() => {
        statusLine.textContent = 'Ready';
        statusLine.classList.remove('sent');
      }, 3000);
    }
  };

  // Hook into form submission to show sent state (after a short delay to simulate send time)
  if (form) {
    const originalSubmit = form.submit.bind(form);
    form.submit = function() {
      setSentState();
      // Call original submit slightly delayed to show the message
      setTimeout(() => originalSubmit(), 200);
    };
  }
});

