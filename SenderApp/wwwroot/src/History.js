import { class_type } from "../fable_modules/fable-library-js.4.28.0/Reflection.js";
import { append, item } from "../fable_modules/fable-library-js.4.28.0/Array.js";

export class JSON$ {
    constructor() {
    }
}

export function JSON$_$reflection() {
    return class_type("SenderApp.Client.JSON", undefined, JSON$);
}

export function JSON_parse_Z721C83C5(json) {
    throw 1;
}

export function JSON_stringify_1505(obj) {
    throw 1;
}

export class LocalStorage {
    constructor() {
    }
}

export function LocalStorage_$reflection() {
    return class_type("SenderApp.Client.LocalStorage", undefined, LocalStorage);
}

export function LocalStorage__getItem_Z721C83C5(this$, key) {
    throw 1;
}

export function LocalStorage__setItem_Z384F8060(this$, key, value) {
    throw 1;
}

export const History_historyKey = "linuxkey-history";

export const History_historyIndexKey = "linuxkey-history-index";

export function History_loadHistoryState() {
    try {
        let json;
        throw 1;
        let matchResult, json_1;
        if (json != null) {
            if (json === "") {
                matchResult = 0;
            }
            else {
                matchResult = 1;
                json_1 = json;
            }
        }
        else {
            matchResult = 0;
        }
        switch (matchResult) {
            case 0:
                return [];
            default:
                return JSON_parse_Z721C83C5(json_1);
        }
    }
    catch (matchValue) {
        return [];
    }
}

export function History_getIndex() {
    try {
        let json;
        throw 1;
        let matchResult, json_1;
        if (json != null) {
            if (json === "") {
                matchResult = 0;
            }
            else {
                matchResult = 1;
                json_1 = json;
            }
        }
        else {
            matchResult = 0;
        }
        switch (matchResult) {
            case 0:
                return 0;
            default:
                return JSON_parse_Z721C83C5(json_1) | 0;
        }
    }
    catch (matchValue) {
        return 0;
    }
}

export function History_saveItems(items) {
    throw 1;
}

export function History_saveIndex(index) {
    throw 1;
}

export function History_addEntry(text) {
    const items = History_loadHistoryState();
    let newItems;
    if (items.length > 0) {
        const lastItem = item(items.length - 1, items);
        try {
            newItems = (((() => {
                throw 1;
            })() === text) ? items : append(items, [(() => {
                throw 1;
            })()]));
        }
        catch (matchValue) {
            newItems = append(items, [(() => {
                throw 1;
            })()]);
        }
    }
    else {
        newItems = [(() => {
            throw 1;
        })()];
    }
    History_saveItems(newItems);
    return newItems;
}

