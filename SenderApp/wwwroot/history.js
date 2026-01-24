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
      return Array.isArray(parsed)
        ? parsed.filter((item) => typeof item === 'string')
        : [];
    } catch {
      return [];
    }
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
    if (items.length === 0 || items[items.length - 1] !== trimmed) {
      items.push(trimmed);
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
  };
})(typeof window !== 'undefined' ? window : globalThis);
