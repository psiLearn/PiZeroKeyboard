import { defaultOf, createAtom } from "../fable_modules/fable-library-js.4.28.0/Util.js";
import { iterateIndexed, isEmpty, item as item_1, length as length_1, empty as empty_1 } from "../fable_modules/fable-library-js.4.28.0/List.js";
import { substring, printf, toText, isNullOrWhiteSpace } from "../fable_modules/fable-library-js.4.28.0/String.js";
import { toString } from "../fable_modules/fable-library-js.4.28.0/Types.js";
import { some, value as value_7 } from "../fable_modules/fable-library-js.4.28.0/Option.js";
import { addHistoryEntry, loadHistoryState, writeHistoryIndex, formatHistoryPreview } from "./History.js";
import { moveNext, movePrev } from "./HistoryCore.js";

export let autoRetryEnabled = createAtom(false);

export let autoRetryTimer = createAtom(undefined);

export let retryCountdownTimer = createAtom(undefined);

export let nextRetryCountdown = createAtom(0);

export let historyItems = createAtom(empty_1());

export let historyIndex = createAtom(0);

export function setDot(element, baseClass, text, cssClass) {
    if (!(element == null)) {
        const cls = isNullOrWhiteSpace(cssClass) ? baseClass : toText(printf("%s %s"))(baseClass)(cssClass);
        element.className = cls;
        element.setAttribute("title", text);
        element.setAttribute("aria-label", text);
    }
}

export function tryGetString(data, prop, fallback) {
    try {
        const value = data[prop];
        return (value == null) ? fallback : toString(value);
    }
    catch (matchValue) {
        return fallback;
    }
}

export function applyStatus(data) {
    const statusEl = document.getElementById("usb-status");
    const capsEl = document.getElementById("caps-status");
    const text = tryGetString(data, "text", "Raspberry Pi USB: unknown");
    const cssClass = tryGetString(data, "cssClass", "unknown");
    const capsText = tryGetString(data, "capsText", "Caps Lock: unknown");
    const capsCssClass = tryGetString(data, "capsCssClass", "unknown");
    setDot(statusEl, "usb-dot", text, cssClass);
    setDot(capsEl, "caps-dot", capsText, capsCssClass);
}

export function refreshStatus() {
    const refreshBtn = document.getElementById("refresh-status");
    if (!(refreshBtn == null)) {
        refreshBtn.disabled = true;
    }
    fetch('/status', { cache: 'no-store' }).then(resp => resp.ok ? resp.json() : Promise.reject(new Error(`HTTP ${resp.status}`))).then(data => ((data) => {
        applyStatus(data);
    })(data)).catch(() => (() => {
        applyStatus({
            text: "Raspberry Pi USB: unknown (refresh failed)",
            cssClass: "unknown",
        });
    })()).finally(() => { if (refreshBtn) refreshBtn.disabled = false; });
}

export function updateRetryCountdown() {
    const el = document.getElementById("retry-countdown");
    if (!(el == null)) {
        let text;
        if (nextRetryCountdown() > 0) {
            const arg = nextRetryCountdown() | 0;
            text = toText(printf("Retrying in %ds…"))(arg);
        }
        else {
            text = "";
        }
        el.textContent = text;
    }
}

export function startRetryCountdown(seconds) {
    nextRetryCountdown(seconds);
    if (retryCountdownTimer() == null) {
    }
    else {
        const id = value_7(retryCountdownTimer());
        (globalThis).clearInterval(id);
    }
    updateRetryCountdown();
    const timer = (globalThis).setInterval((() => {
        nextRetryCountdown(nextRetryCountdown() - 1);
        updateRetryCountdown();
        if (nextRetryCountdown() <= 0) {
            if (retryCountdownTimer() == null) {
            }
            else {
                const id_1 = value_7(retryCountdownTimer());
                (globalThis).clearInterval(id_1);
            }
            retryCountdownTimer(undefined);
        }
    }), 1000);
    retryCountdownTimer(some(timer));
}

export function initCopyButton() {
    const btn = document.getElementById("copy-target");
    if (!(btn == null)) {
        btn.addEventListener("click", ((event) => {
            event.preventDefault();
            const display = document.getElementById("target-display");
            if (!(display == null)) {
                const text = toString(display.textContent);
                const clipboard = (globalThis).navigator.clipboard;
                if (!(clipboard == null)) {
                    const promise = clipboard.writeText(text);
                    (promise.then((_arg) => {
                        const originalText = btn.textContent;
                        btn.textContent = "✓";
                        (globalThis).setTimeout((() => {
                            btn.textContent = originalText;
                        }), 1500);
                    })).catch((err) => {
                        console.error(some("Failed to copy:"), ...err);
                    });
                }
            }
        }));
    }
}

export function initStatusRefresh() {
    const refreshBtn = document.getElementById("refresh-status");
    if (!(refreshBtn == null)) {
        refreshBtn.addEventListener("click", ((event) => {
            event.preventDefault();
            refreshStatus();
        }));
    }
}

export function initAutoRetry() {
    const checkbox = document.getElementById("auto-retry");
    if (!(checkbox == null)) {
        checkbox.addEventListener("change", ((_arg) => {
            autoRetryEnabled(checkbox.checked);
            if (autoRetryEnabled()) {
                if (autoRetryTimer() == null) {
                }
                else {
                    const id = value_7(autoRetryTimer());
                    (globalThis).clearInterval(id);
                }
                const timer = (globalThis).setInterval((() => {
                    const statusEl = document.getElementById("usb-status");
                    if (!((statusEl == null) ? false : (() => {
                        try {
                            const classList = statusEl.classList;
                            return classList.contains("connected");
                        }
                        catch (matchValue) {
                            return false;
                        }
                    })())) {
                        refreshStatus();
                        startRetryCountdown(5);
                    }
                }), 5000);
                autoRetryTimer(some(timer));
            }
            else {
                if (autoRetryTimer() == null) {
                }
                else {
                    const id_1 = value_7(autoRetryTimer());
                    (globalThis).clearInterval(id_1);
                }
                autoRetryTimer(undefined);
                if (retryCountdownTimer() == null) {
                }
                else {
                    const id_2 = value_7(retryCountdownTimer());
                    (globalThis).clearInterval(id_2);
                }
                retryCountdownTimer(undefined);
                nextRetryCountdown(0);
                updateRetryCountdown();
            }
        }));
    }
}

export function setupWebSocketConnection() {
    if ((() => {
        try {
            return !((globalThis).WebSocket == null);
        }
        catch (matchValue) {
            return false;
        }
    })()) {
        try {
            const scheme = (toString((globalThis).location.protocol) === "https:") ? "wss:" : "ws:";
            const host = toString((globalThis).location.host);
            const url = toText(printf("%s//%s/status/ws"))(scheme)(host);
            const socket = new WebSocket(url);
            socket.onmessage = ((event) => {
                try {
                    applyStatus(JSON.parse(toString(event.data)));
                }
                catch (matchValue_1) {
                    applyStatus({
                        text: "Raspberry Pi USB: unknown",
                        cssClass: "unknown",
                    });
                }
            });
            socket.onerror = ((_arg) => (socket.close()));
            socket.onclose = ((_arg_1) => {
                (globalThis).setTimeout((() => {
                    setupWebSocketConnection();
                }), 3000);
            });
        }
        catch (matchValue_2) {
            (globalThis).setTimeout((() => {
                setupWebSocketConnection();
            }), 3000);
        }
    }
}

export function insertToken(token) {
    const textarea = document.getElementById("text");
    if (!(textarea == null) && !isNullOrWhiteSpace(token)) {
        const start_1 = textarea.selectionStart | 0;
        const finish = textarea.selectionEnd | 0;
        const currentValue = toString(textarea.value);
        const newValue = (substring(currentValue, 0, start_1) + token) + substring(currentValue, finish);
        textarea.value = newValue;
        const caret = (start_1 + token.length) | 0;
        textarea.setSelectionRange(caret, caret);
        textarea.focus();
    }
}

export function initTokenButtons() {
    const buttons = document.querySelectorAll("[data-token]");
    if (!(buttons == null)) {
        const length = buttons.length | 0;
        for (let i = 0; i <= (length - 1); i++) {
            const button = buttons.item(i);
            if (!(button == null)) {
                button.addEventListener("click", ((event) => {
                    event.preventDefault();
                    insertToken(toString(button.dataset.token));
                }));
            }
        }
    }
}

export function updateHistoryButtons() {
    const historyBack = document.getElementById("history-prev");
    const historyForward = document.getElementById("history-next");
    if (!(historyBack == null)) {
        historyBack.disabled = (historyIndex() <= 0);
    }
    if (!(historyForward == null)) {
        historyForward.disabled = (historyIndex() >= (length_1(historyItems()) - 1));
    }
}

export function applyHistory() {
    const textarea = document.getElementById("text");
    if ((!(textarea == null) && (historyIndex() >= 0)) && (historyIndex() < length_1(historyItems()))) {
        const value = item_1(historyIndex(), historyItems()).text;
        textarea.value = value;
        const caret = value.length | 0;
        textarea.setSelectionRange(caret, caret);
        textarea.focus();
    }
}

export function renderHistoryList() {
    const historyList = document.getElementById("history-list");
    if (!(historyList == null)) {
        historyList.innerHTML = "";
        if (isEmpty(historyItems())) {
            const empty = document.createElement("div");
            empty.className = "history-empty";
            empty.textContent = "No history yet.";
            historyList.appendChild(empty);
        }
        else {
            iterateIndexed((index, item) => {
                const button = document.createElement("button");
                button.type = "button";
                button.className = "history-item";
                if (index === historyIndex()) {
                    const classList = button.classList;
                    classList.add("active");
                }
                button.textContent = formatHistoryPreview(item);
                button.addEventListener("click", ((event) => {
                    event.preventDefault();
                    historyIndex(index);
                    writeHistoryIndex(historyIndex());
                    applyHistory();
                    updateHistoryButtons();
                    renderHistoryList();
                    const historyClassList = historyList.classList;
                    historyClassList.add("hidden");
                }));
                historyList.appendChild(button);
            }, historyItems());
        }
    }
}

export function refreshHistoryState() {
    const state = loadHistoryState();
    historyItems(state.items);
    historyIndex(state.index);
    updateHistoryButtons();
    renderHistoryList();
}

export function initHistoryNavigation() {
    const historyBack = document.getElementById("history-prev");
    const historyForward = document.getElementById("history-next");
    if (!(historyBack == null) ? true : !(historyForward == null)) {
        refreshHistoryState();
        if (!(historyBack == null)) {
            historyBack.addEventListener("click", ((event) => {
                event.preventDefault();
                if (!isEmpty(historyItems())) {
                    historyIndex(movePrev(historyIndex(), historyItems()));
                    writeHistoryIndex(historyIndex());
                    applyHistory();
                    updateHistoryButtons();
                    renderHistoryList();
                }
            }));
        }
        if (!(historyForward == null)) {
            historyForward.addEventListener("click", ((event_1) => {
                event_1.preventDefault();
                if (!isEmpty(historyItems())) {
                    historyIndex(moveNext(historyIndex(), historyItems()));
                    writeHistoryIndex(historyIndex());
                    applyHistory();
                    updateHistoryButtons();
                    renderHistoryList();
                }
            }));
        }
    }
}

export function initHistoryToggle() {
    const historyToggle = document.getElementById("history-toggle");
    const historyList = document.getElementById("history-list");
    if (!(historyToggle == null) && !(historyList == null)) {
        historyToggle.addEventListener("click", ((event) => {
            event.preventDefault();
            renderHistoryList();
            const classList = historyList.classList;
            classList.toggle("hidden");
        }));
    }
}

export function initKeyboardShortcuts() {
    const textarea = document.getElementById("text");
    const form = (textarea == null) ? defaultOf() : (textarea.closest("form"));
    if (!(textarea == null)) {
        textarea.addEventListener("keydown", ((event) => {
            if ((event.ctrlKey ? true : event.metaKey) && (toString(event.key) === "Enter")) {
                event.preventDefault();
                const sendBtn = document.getElementById("send-text");
                if (!(sendBtn == null) && !sendBtn.disabled) {
                    form.submit();
                }
            }
        }));
    }
}

export function initFormSubmitHandler() {
    const textarea = document.getElementById("text");
    const form = (textarea == null) ? defaultOf() : (textarea.closest("form"));
    const statusLine = document.getElementById("status-line");
    if (!(form == null)) {
        form.addEventListener("submit", ((_arg) => {
            if (!(statusLine == null)) {
                statusLine.textContent = "Sending...";
                const statusClassList = statusLine.classList;
                statusClassList.add("sending");
                statusClassList.remove("sent");
            }
            const privateSendCheckbox = document.getElementById("private-send");
            if (!((privateSendCheckbox == null) ? false : privateSendCheckbox.checked)) {
                const state = addHistoryEntry(toString(textarea.value));
                historyItems(state.items);
                historyIndex(state.index);
                updateHistoryButtons();
                renderHistoryList();
            }
            (globalThis).setTimeout((() => {
                if (!(statusLine == null)) {
                    statusLine.textContent = "Sent ✓";
                    const statusClassList_1 = statusLine.classList;
                    statusClassList_1.remove("sending");
                    statusClassList_1.add("sent");
                    (globalThis).setTimeout((() => {
                        statusLine.textContent = "Ready";
                        const statusClassList_2 = statusLine.classList;
                        statusClassList_2.remove("sent");
                    }), 3000);
                }
            }), 200);
        }));
    }
}

export function init() {
    initCopyButton();
    initStatusRefresh();
    initAutoRetry();
    setupWebSocketConnection();
    initTokenButtons();
    initHistoryNavigation();
    initHistoryToggle();
    initKeyboardShortcuts();
    initFormSubmitHandler();
    refreshStatus();
}

export function start() {
    if (toString((document).readyState) === "loading") {
        (document).addEventListener("DOMContentLoaded", ((_arg) => {
            init();
        }));
    }
    else {
        init();
    }
}

start();

