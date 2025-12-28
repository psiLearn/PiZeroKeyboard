#!/usr/bin/env python3
"""
Lightweight receiver for Raspberry Pi Zero (armv6).
Listens on TCP, maps text to HID reports, and writes to /dev/hidg0.
"""

import socket
import sys
import time
from typing import Dict, Optional, Tuple

TIMEOUT_SECONDS = 5.0
MAX_PAYLOAD_BYTES = 256_000

SHIFT = 0x02

# Modifier, Key
HID_KEY_MAP: Dict[str, Tuple[int, int]] = {
    "a": (0, 0x04),
    "b": (0, 0x05),
    "c": (0, 0x06),
    "d": (0, 0x07),
    "e": (0, 0x08),
    "f": (0, 0x09),
    "g": (0, 0x0A),
    "h": (0, 0x0B),
    "i": (0, 0x0C),
    "j": (0, 0x0D),
    "k": (0, 0x0E),
    "l": (0, 0x0F),
    "m": (0, 0x10),
    "n": (0, 0x11),
    "o": (0, 0x12),
    "p": (0, 0x13),
    "q": (0, 0x14),
    "r": (0, 0x15),
    "s": (0, 0x16),
    "t": (0, 0x17),
    "u": (0, 0x18),
    "v": (0, 0x19),
    "w": (0, 0x1A),
    "x": (0, 0x1B),
    "y": (0, 0x1C),
    "z": (0, 0x1D),
    "1": (0, 0x1E),
    "2": (0, 0x1F),
    "3": (0, 0x20),
    "4": (0, 0x21),
    "5": (0, 0x22),
    "6": (0, 0x23),
    "7": (0, 0x24),
    "8": (0, 0x25),
    "9": (0, 0x26),
    "0": (0, 0x27),
    "\n": (0, 0x28),
    "\t": (0, 0x2B),
    " ": (0, 0x2C),
    "-": (0, 0x2D),
    "=": (0, 0x2E),
    "[": (0, 0x2F),
    "]": (0, 0x30),
    "\\": (0, 0x31),
    ";": (0, 0x33),
    "'": (0, 0x34),
    "`": (0, 0x35),
    ",": (0, 0x36),
    ".": (0, 0x37),
    "/": (0, 0x38),
    "_": (SHIFT, 0x2D),
    "+": (SHIFT, 0x2E),
    "{": (SHIFT, 0x2F),
    "}": (SHIFT, 0x30),
    "|": (SHIFT, 0x31),
    ":": (SHIFT, 0x33),
    '"': (SHIFT, 0x34),
    "~": (SHIFT, 0x35),
    "<": (SHIFT, 0x36),
    ">": (SHIFT, 0x37),
    "?": (SHIFT, 0x38),
    "!": (SHIFT, 0x1E),
    "@": (SHIFT, 0x1F),
    "#": (SHIFT, 0x20),
    "$": (SHIFT, 0x21),
    "%": (SHIFT, 0x22),
    "^": (SHIFT, 0x23),
    "&": (SHIFT, 0x24),
    "*": (SHIFT, 0x25),
    "(": (SHIFT, 0x26),
    ")": (SHIFT, 0x27),
}


def to_hid(ch: str) -> Optional[Tuple[int, int]]:
    normalized = "\n" if ch == "\r" else ch
    if normalized.isalpha() and normalized.isupper():
        base = HID_KEY_MAP.get(normalized.lower())
        if base:
            return base[0] | SHIFT, base[1]
        return None
    return HID_KEY_MAP.get(normalized)


def create_sender(dev_path: str):
    try:
        hid = open(dev_path, "wb", buffering=0)
    except OSError as ex:
        sys.stderr.write(f"Failed to open HID device at {dev_path}: {ex}\n")
        sys.exit(1)

    def send(mod: int, key: int):
        press = bytes([mod, 0x00, key, 0x00, 0x00, 0x00, 0x00, 0x00])
        release = b"\x00" * 8
        hid.write(press)
        hid.flush()
        time.sleep(0.005)
        hid.write(release)
        hid.flush()
        time.sleep(0.005)

    return send


def handle_text(send, text: str):
    for ch in text:
        mapping = to_hid(ch)
        if mapping:
            send(*mapping)
        else:
            sys.stderr.write(f"Skipping unsupported char '{ch}' (0x{ord(ch):04X})\n")
            sys.stderr.flush()


def run_server(port: int, dev_path: str, emulate: bool, max_bytes: Optional[int] = None):
    send = (lambda mod, key: None) if emulate else create_sender(dev_path)
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as server:
        server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        server.settimeout(TIMEOUT_SECONDS)
        server.bind(("", port))
        server.listen(1)
        print(f"Receiver listening on {port} (emulate={emulate}, hid={dev_path})")
        max_len = max_bytes if max_bytes is not None else MAX_PAYLOAD_BYTES
        while True:
            conn, addr = server.accept()
            conn.settimeout(TIMEOUT_SECONDS)
            with conn:
                print(f"Client connected: {addr}")
                chunks = []
                total = 0
                try:
                    while True:
                        chunk = conn.recv(4096)
                        if not chunk:
                            break
                        total += len(chunk)
                        if total > max_len:
                            raise RuntimeError("Payload too large")
                        chunks.append(chunk)
                except socket.timeout:
                    sys.stderr.write("Connection timed out.\n")
                    sys.stderr.flush()
                    continue
                except Exception as ex:  # noqa: BLE001
                    sys.stderr.write(f"Error receiving payload: {ex}\n")
                    sys.stderr.flush()
                    continue

                try:
                    text = b"".join(chunks).decode("utf-8", errors="strict")
                except UnicodeDecodeError as ex:
                    sys.stderr.write(f"Invalid UTF-8 payload: {ex}\n")
                    sys.stderr.flush()
                    continue

                print(f"Received {len(text)} characters")
                if emulate:
                    print("[EMULATED OUTPUT]")
                    print(text)
                try:
                    handle_text(send, text)
                except Exception as ex:  # noqa: BLE001
                    sys.stderr.write(f"HID handling error: {ex}\n")
                    sys.stderr.flush()


def main():
    port = 5000
    dev_path = "/dev/hidg0"
    emulate = False
    max_bytes_env = None

    args = sys.argv[1:]
    while args:
        arg = args.pop(0)
        if arg == "--emulate":
            emulate = True
        elif arg.startswith("--hid-path="):
            dev_path = arg.split("=", 1)[1]
        elif arg.startswith("--max-bytes="):
            try:
                max_bytes_env = int(arg.split("=", 1)[1])
            except ValueError:
                sys.stderr.write("Invalid --max-bytes value; ignoring.\n")
        elif arg.isdigit():
            port = int(arg)
        else:
            sys.stderr.write(f"Ignoring unknown arg: {arg}\n")

    max_bytes = max_bytes_env if max_bytes_env else None
    run_server(port, dev_path, emulate, max_bytes=max_bytes)


if __name__ == "__main__":
    main()
