import { Record } from "../fable_modules/fable-library-js.4.28.0/Types.js";
import { int32_type, list_type, record_type, option_type, float64_type, string_type } from "../fable_modules/fable-library-js.4.28.0/Reflection.js";
import { max, min } from "../fable_modules/fable-library-js.4.28.0/Double.js";
import { isNullOrWhiteSpace } from "../fable_modules/fable-library-js.4.28.0/String.js";
import { singleton, append, tryLast, skip, length, isEmpty } from "../fable_modules/fable-library-js.4.28.0/List.js";
import { map, defaultArg } from "../fable_modules/fable-library-js.4.28.0/Option.js";

export class HistoryItem extends Record {
    constructor(text, timestamp) {
        super();
        this.text = text;
        this.timestamp = timestamp;
    }
}

export function HistoryItem_$reflection() {
    return record_type("SenderApp.Client.HistoryCore.HistoryItem", [], HistoryItem, () => [["text", string_type], ["timestamp", option_type(float64_type)]]);
}

export class HistoryState extends Record {
    constructor(items, index) {
        super();
        this.items = items;
        this.index = (index | 0);
    }
}

export function HistoryState_$reflection() {
    return record_type("SenderApp.Client.HistoryCore.HistoryState", [], HistoryState, () => [["items", list_type(HistoryItem_$reflection())], ["index", int32_type]]);
}

export const maxHistoryItems = 50;

export function clampIndex(index, maxIndex) {
    if (maxIndex < 0) {
        return 0;
    }
    else {
        return min(max(index, 0), maxIndex) | 0;
    }
}

export function normalizeText(text) {
    if (text == null) {
        return undefined;
    }
    else {
        const trimmed = text.trim();
        if (isNullOrWhiteSpace(trimmed)) {
            return undefined;
        }
        else {
            return trimmed;
        }
    }
}

function lastIndex(items) {
    if (isEmpty(items)) {
        return 0;
    }
    else {
        return (length(items) - 1) | 0;
    }
}

function pruneHistory(items) {
    if (length(items) > maxHistoryItems) {
        return skip(length(items) - maxHistoryItems, items);
    }
    else {
        return items;
    }
}

export function addEntry(now, items, trimmed) {
    const updated = (isEmpty(items) ? true : (defaultArg(map((item) => item.text, tryLast(items)), "") !== trimmed)) ? pruneHistory(append(items, singleton(new HistoryItem(trimmed, now())))) : items;
    return new HistoryState(updated, lastIndex(updated));
}

export function movePrev(index, items) {
    if (isEmpty(items)) {
        return 0;
    }
    else {
        return clampIndex(index - 1, length(items) - 1) | 0;
    }
}

export function moveNext(index, items) {
    if (isEmpty(items)) {
        return 0;
    }
    else {
        return clampIndex(index + 1, length(items) - 1) | 0;
    }
}

