import { class_type } from "../fable_modules/fable-library-js.4.28.0/Reflection.js";
import { defaultOf, createAtom } from "../fable_modules/fable-library-js.4.28.0/Util.js";
import { printf, toText } from "../fable_modules/fable-library-js.4.28.0/String.js";

export class Document$ {
    constructor() {
    }
}

export function Document$_$reflection() {
    return class_type("SenderApp.Client.Document", undefined, Document$);
}

export function Document_getElementById_Z721C83C5(id) {
    throw 1;
}

export function Document_querySelectorAll_Z721C83C5(selector) {
    throw 1;
}

export function Document_get_readyState() {
    throw 1;
}

export function Document_addEventListener_2BF3B0AA(event, handler) {
    throw 1;
}

export class GlobalThis {
    constructor() {
    }
}

export function GlobalThis_$reflection() {
    return class_type("SenderApp.Client.GlobalThis", undefined, GlobalThis);
}

export function GlobalThis_get_location() {
    throw 1;
}

export function GlobalThis_get_navigator() {
    throw 1;
}

export function GlobalThis_setTimeout_Z240017D3(fn, ms) {
    throw 1;
}

export function GlobalThis_setInterval_Z240017D3(fn, ms) {
    throw 1;
}

export function GlobalThis_clearInterval_5E38073B(id) {
    throw 1;
}

export let Sender_autoRetryEnabled = createAtom(false);

export let Sender_autoRetryTimer = createAtom(undefined);

export let Sender_retryCountdownTimer = createAtom(undefined);

export let Sender_nextRetryCountdown = createAtom(0);

export let Sender_historyItems = createAtom([]);

export let Sender_historyIndex = createAtom(0);

export function Sender_setDot(element, baseClass, text, cssClass) {
    if (!(element == null)) {
        const className = (cssClass.length > 0) ? toText(printf("%s %s"))(baseClass)(cssClass) : baseClass;
        throw 1;
        throw 1;
        throw 1;
    }
}

export function Sender_applyStatus(data) {
    Sender_setDot((() => {
        throw 1;
    })(), "usb-dot", (() => {
        try {
            let t;
            throw 1;
            return (t == null) ? "Raspberry Pi USB: unknown" : t;
        }
        catch (matchValue) {
            return "Raspberry Pi USB: unknown";
        }
    })(), (() => {
        try {
            let c;
            throw 1;
            return (c == null) ? "unknown" : c;
        }
        catch (matchValue_1) {
            return "unknown";
        }
    })());
    Sender_setDot((() => {
        throw 1;
    })(), "caps-dot", (() => {
        try {
            let t_1;
            throw 1;
            return (t_1 == null) ? "Caps Lock: unknown" : t_1;
        }
        catch (matchValue_2) {
            return "Caps Lock: unknown";
        }
    })(), (() => {
        try {
            let c_1;
            throw 1;
            return (c_1 == null) ? "unknown" : c_1;
        }
        catch (matchValue_3) {
            return "unknown";
        }
    })());
}

export function Sender_refreshStatus() {
    defaultOf();
}

export let Sender_data = createAtom(defaultOf());

throw 1;

if (!(Sender_data() == null)) {
    Sender_applyStatus(Sender_data());
}
else {
    Sender_applyStatus((() => {
        throw 1;
    })());
}

