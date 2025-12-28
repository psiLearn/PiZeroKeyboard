#!/usr/bin/env python3
"""
Minimal sender for Raspberry Pi Zero (armv6) or any host with Python 3.
CLI: python sender.py <ip> <port> "text"
Web UI (requires Flask): python sender.py serve <ip> <port> [--host 0.0.0.0] [--web-port 8080]
"""

import argparse
import socket
import sys
from typing import Optional


def send_once(ip: str, port: int, text: str) -> bool:
    payload = text.encode("utf-8")
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
        sock.connect((ip, port))
        sock.sendall(payload)
    print(f"Sent {len(payload)} bytes to {ip}:{port}")
    return True


def build_flask_app(target_ip: str, target_port: int, token: Optional[str]):
    try:
        from flask import Flask, request, render_template_string, redirect
    except ImportError:
        sys.stderr.write("Flask is not installed. Install with: pip install -r requirements-pizero.txt\n")
        sys.exit(1)

    template = """
<!doctype html>
<html>
<head>
  <meta charset="utf-8">
  <title>LinuxKey Sender</title>
  <style>
    body { font-family: Arial, sans-serif; margin: 2rem auto; max-width: 42rem; }
    textarea { width: 100%; min-height: 12rem; font-family: Consolas, monospace; }
    button { padding: 0.6rem 1.2rem; }
    .status { margin: 0.5rem 0; }
  </style>
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <script>if (!window.history.replaceState) {}</script>
</head>
<body>
  <h1>LinuxKey Sender</h1>
  <p>Target: {{ ip }}:{{ port }}</p>
  {% if status %}
    <div class="status">{{ status }}</div>
  {% endif %}
  <form method="post" action="/">
    <textarea name="text" placeholder="Paste text here...">{{ text }}</textarea><br/>
    {% if token_required %}
      <input type="password" name="token" placeholder="Token" />
    {% endif %}
    <button type="submit">Send</button>
  </form>
</body>
</html>
"""

    app = Flask(__name__)

    @app.route("/", methods=["GET", "POST"])
    def index():
        status: Optional[str] = None
        text_val = ""
        if request.method == "POST":
            if token and request.form.get("token") != token:
                return "Unauthorized", 401
            text_val = (request.form.get("text") or "").rstrip()
            if text_val:
                try:
                    send_once(target_ip, target_port, text_val)
                    status = f"Sent {len(text_val.encode('utf-8'))} bytes."
                    text_val = ""
                except Exception as ex:  # noqa: BLE001
                    status = f"Failed: {ex}"
            else:
                status = "Please enter some text."
        return render_template_string(
            template,
            ip=target_ip,
            port=target_port,
            status=status,
            text=text_val,
            token_required=bool(token),
        )

    @app.route("/healthz")
    def healthz():
        return "OK", 200

    return app


def main():
    parser = argparse.ArgumentParser(description="LinuxKey sender for Pi Zero")
    subparsers = parser.add_subparsers(dest="mode")

    cli_parser = subparsers.add_parser("cli", help="Send a single payload (default)")
    cli_parser.add_argument("ip")
    cli_parser.add_argument("port", type=int)
    cli_parser.add_argument("text")

    web_parser = subparsers.add_parser("serve", help="Start web UI (requires Flask)")
    web_parser.add_argument("ip")
    web_parser.add_argument("port", type=int)
    web_parser.add_argument("--host", default="127.0.0.1")
    web_parser.add_argument("--web-port", type=int, default=8080)
    web_parser.add_argument("--token", help="Optional token required to submit text")

    args = parser.parse_args()

    if args.mode in (None, "cli"):
        ip = args.ip if args.mode else sys.argv[1] if len(sys.argv) > 1 else "127.0.0.1"
        try:
            port = args.port if args.mode else int(sys.argv[2])
            text = args.text if args.mode else sys.argv[3]
        except Exception:
            parser.print_usage()
            sys.exit(1)
        send_once(ip, port, text)
    else:
        app = build_flask_app(args.ip, args.port, args.token)
        app.run(host=args.host, port=args.web_port)


if __name__ == "__main__":
    main()
