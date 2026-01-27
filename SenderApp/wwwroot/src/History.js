import { printf, toText, substring, isNullOrWhiteSpace } from "../fable_modules/fable-library-js.4.28.0/String.js";
import { toString } from "../fable_modules/fable-library-js.4.28.0/Types.js";
import { length, isEmpty, map, toArray, ofArray, choose, empty } from "../fable_modules/fable-library-js.4.28.0/List.js";
import { addEntry, normalizeText, HistoryState, clampIndex, HistoryItem } from "./HistoryCore.js";
import { equals, int32ToString, defaultOf } from "../fable_modules/fable-library-js.4.28.0/Util.js";
import { parse } from "../fable_modules/fable-library-js.4.28.0/Int32.js";

function StorageOps_tryGetItem(key) {
    if (isNullOrWhiteSpace(key)) {
        return undefined;
    }
    else {
        try {
            const value = (window.localStorage).getItem(key);
            return (value == null) ? undefined : toString(value);
        }
        catch (matchValue) {
            return undefined;
        }
    }
}

function StorageOps_trySetItem(key, value) {
    if (isNullOrWhiteSpace(key)) {
    }
    else {
        try {
            (window.localStorage).setItem(key, value);
        }
        catch (matchValue) {
        }
    }
}

export const historyKey = "linuxkey-history";

export const historyIndexKey = "linuxkey-history-index";

function parseItems(raw) {
    if (raw != null) {
        const json = raw;
        try {
            return choose((item) => {
                if (item == null) {
                    return undefined;
                }
                else if ((typeof item) === "string") {
                    return new HistoryItem(toString(item), Date.now());
                }
                else {
                    const textObj = item.text;
                    if (textObj == null) {
                        return undefined;
                    }
                    else {
                        const text = toString(textObj);
                        const tsObj = item.timestamp;
                        return new HistoryItem(text, (tsObj == null) ? undefined : (() => {
                            try {
                                return tsObj;
                            }
                            catch (matchValue) {
                                return undefined;
                            }
                        })());
                    }
                }
            }, ofArray(JSON.parse(json)));
        }
        catch (matchValue_1) {
            return empty();
        }
    }
    else {
        return empty();
    }
}

function toJsItem(item) {
    let matchValue;
    return {
        text: item.text,
        timestamp: (matchValue = item.timestamp, (matchValue != null) ? matchValue : defaultOf()),
    };
}

export function readHistory() {
    return parseItems(StorageOps_tryGetItem(historyKey));
}

export function writeHistory(items) {
    const jsItems = toArray(map(toJsItem, items));
    try {
        StorageOps_trySetItem(historyKey, JSON.stringify(jsItems));
    }
    catch (matchValue) {
    }
}

export function readHistoryIndex(maxIndex) {
    if (maxIndex < 0) {
        return 0;
    }
    else {
        const matchValue = StorageOps_tryGetItem(historyIndexKey);
        let matchResult, raw;
        if (matchValue != null) {
            if (matchValue === "") {
                matchResult = 0;
            }
            else {
                matchResult = 1;
                raw = matchValue;
            }
        }
        else {
            matchResult = 0;
        }
        switch (matchResult) {
            case 0:
                return maxIndex | 0;
            default:
                try {
                    return clampIndex(parse(raw, 511, false, 32), maxIndex) | 0;
                }
                catch (matchValue_1) {
                    return clampIndex(0, maxIndex) | 0;
                }
        }
    }
}

export function writeHistoryIndex(index) {
    StorageOps_trySetItem(historyIndexKey, int32ToString(index));
}

export function loadHistoryState() {
    const items = readHistory();
    if (isEmpty(items)) {
        return new HistoryState(empty(), 0);
    }
    else {
        return new HistoryState(items, readHistoryIndex(length(items) - 1));
    }
}

export function addHistoryEntry(text) {
    const matchValue = normalizeText(text);
    if (matchValue != null) {
        const trimmed = matchValue;
        const items = readHistory();
        const state = addEntry(() => Date.now(), items, trimmed);
        if (!equals(state.items, items)) {
            writeHistory(state.items);
        }
        writeHistoryIndex(state.index);
        return state;
    }
    else {
        return loadHistoryState();
    }
}

export function formatHistoryPreview(item) {
    const preview = (item.text.length > 30) ? (substring(item.text, 0, 30) + "…") : item.text;
    const matchValue = item.timestamp;
    if (matchValue != null) {
        const ts = matchValue;
        try {
            const date = new Date(ts);
            const hours = (date.getHours()) | 0;
            const minutes = (date.getMinutes()) | 0;
            const seconds = (date.getSeconds()) | 0;
            return toText(printf("%02d:%02d:%02d | %s"))(hours)(minutes)(seconds)(preview);
        }
        catch (matchValue_1) {
            return preview;
        }
    }
    else {
        return preview;
    }
}

