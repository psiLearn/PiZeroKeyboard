(function (root) {
  'use strict';

  const safeGetItem = (storage, key) => {
    if (!storage || !key) return null;
    try {
      return storage.getItem(key);
    } catch {
      return null;
    }
  };

  const safeSetItem = (storage, key, value) => {
    if (!storage || !key) return;
    try {
      storage.setItem(key, value);
    } catch {
      // ignore storage failures
    }
  };

  const parseItems = (raw) => {
    if (!raw) return [];
    try {
      const parsed = JSON.parse(raw);
      if (!Array.isArray(parsed)) return [];
      
      // Support both old string format and new object format
      return parsed.map((item) => {
        if (typeof item === 'string') {
          // Migrate old string format to new object format
          return { text: item, timestamp: Date.now() };
        }
        if (item && typeof item === 'object' && item.text) {
          // Already in new format
          return { text: item.text, timestamp: item.timestamp || Date.now() };
        }
        return null;
      }).filter(item => item !== null);
    } catch {
      return [];
    }
  };

  const formatHistoryPreview = (item) => {
    if (typeof item === 'string') {
      return item; // fallback for old format
    }
    const text = item.text || '';
    const preview = text.length > 30 ? text.substring(0, 30) + '…' : text;
    
    let timeStr = '';
    if (item.timestamp) {
      const date = new Date(item.timestamp);
      const hours = String(date.getHours()).padStart(2, '0');
      const minutes = String(date.getMinutes()).padStart(2, '0');
      const seconds = String(date.getSeconds()).padStart(2, '0');
      timeStr = `${hours}:${minutes}:${seconds} | `;
    }
    
    return timeStr + preview;
  };

  const readHistory = (storage, historyKey) => {
    return parseItems(safeGetItem(storage, historyKey));
  };

  const writeHistory = (storage, historyKey, items) => {
    safeSetItem(storage, historyKey, JSON.stringify(items));
  };

  const clampIndex = (index, maxIndex) => {
    if (maxIndex < 0) return 0;
    if (Number.isNaN(index)) return maxIndex;
    return Math.min(Math.max(index, 0), maxIndex);
  };

  const readHistoryIndex = (storage, indexKey, maxIndex) => {
    if (maxIndex < 0) return 0;
    const raw = safeGetItem(storage, indexKey);
    if (raw === null || raw === undefined || raw === '') return maxIndex;
    const parsed = Number(raw);
    return clampIndex(parsed, maxIndex);
  };

  const writeHistoryIndex = (storage, indexKey, index) => {
    safeSetItem(storage, indexKey, String(index));
  };

  const loadHistoryState = (storage, historyKey, indexKey) => {
    const items = readHistory(storage, historyKey);
    if (items.length === 0) {
      return { items, index: 0 };
    }
    const maxIndex = items.length - 1;
    const index = readHistoryIndex(storage, indexKey, maxIndex);
    return { items, index };
  };

  const addHistoryEntry = (storage, historyKey, indexKey, text) => {
    const trimmed = (text || '').trim();
    if (!trimmed) {
      return loadHistoryState(storage, historyKey, indexKey);
    }

    const items = readHistory(storage, historyKey);
    const lastItem = items.length > 0 ? items[items.length - 1] : null;
    const lastText = lastItem ? (lastItem.text || lastItem) : '';
    
    if (items.length === 0 || lastText !== trimmed) {
      items.push({ text: trimmed, timestamp: Date.now() });
      writeHistory(storage, historyKey, items);
    }

    const index = items.length > 0 ? items.length - 1 : 0;
    if (items.length > 0) {
      writeHistoryIndex(storage, indexKey, index);
    }
    return { items, index };
  };

  root.LinuxKeyHistory = {
    readHistory,
    writeHistory,
    readHistoryIndex,
    writeHistoryIndex,
    loadHistoryState,
    addHistoryEntry,
    clampIndex,
    formatHistoryPreview,
  };
})(typeof window !== 'undefined' ? window : globalThis);
