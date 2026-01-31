# LinuxKey

Windows sender and Raspberry Pi Zero receiver written in F# for transmitting text over TCP and replaying it as USB HID keyboard events.

## Projects

- `SenderApp` - Windows console + web UI sender. Run `dotnet run -- <pi-ip> <port> "text"` for a one-off send, or `dotnet run -- <pi-ip> <port>` for the UI.
- `SenderApp.Tests` - Sender unit/integration tests (configuration, handlers, views, WebSocket status, receiver client).
- `ReceiverApp` – Raspberry Pi console app that listens for TCP connections, converts characters to HID codes, and writes them to `/dev/hidg0`.
- `PiSetup/setup-hid-gadget.sh` – Helper script that configures the Pi Zero USB gadget as a keyboard (run once per boot as root).

## Build

```bash
dotnet build SenderApp
dotnet build ReceiverApp
```

### Client build (Fable)

The sender UI client can be rebuilt from `SenderApp/Client` (Fable + npm). `SenderApp.fsproj` runs `npm install`/`npm run build` before build (best-effort), but you can run it explicitly:

```powershell
.\build-client.ps1
```

If Node/npm are missing, the build will still succeed but the UI bundle may be stale.

### Utility scripts

- `build-client.ps1` – rebuilds the Fable client bundle.
- `validate-history.ps1` – quick validation for `SenderApp/Client/src/History.fs` (checks for key types/functions).

## Deploy on Raspberry Pi

1. Ensure OTG mode is enabled in `/boot/config.txt` and `/boot/cmdline.txt` by loading the `dwc2` driver.
2. On Pi Zero 2 W, disable the built-in `dwc_otg` host driver by adding `initcall_blacklist=dwc_otg_driver_init` to `/boot/cmdline.txt`, then reboot.
3. Run `sudo /opt/hid/setup-hid-gadget.sh` after copying the script to the Pi to expose `/dev/hidg0`.
4. Publish the receiver with `dotnet publish ReceiverApp -c Release -r linux-arm --self-contained false` and run it with `sudo dotnet ReceiverApp.dll 5000`.

### Raspberry Pi Zero 2 W (armv7) fresh install with Docker (recommended)

1. Flash Raspberry Pi OS Lite (32-bit) to a fresh SD card with Raspberry Pi Imager.
   - In Imager, open the OS customization (gear icon) and set:
     - Hostname (for example `linuxkey`)
     - Enable SSH (password or public key)
     - Configure Wi-Fi (SSID, password, country)
     - Username and password
   - Write the image to the SD card.
2. Mount the SD card on Windows. In the boot partition:
   - Add `dtoverlay=dwc2,dr_mode=peripheral` to `config.txt`.
   - Append `modules-load=dwc2 initcall_blacklist=dwc_otg_driver_init` to the single-line `cmdline.txt` (keep it as one line).
   - Optional: run `pi-zero/init-sd.ps1 -BootDrive E: -ForceDwc2` to apply these settings.
3. Insert the SD card into the Pi Zero 2 W and boot. Find the IP (router/DHCP list).
4. On Windows, deploy the Docker stack over SSH:
   - `.\deploy-docker-ssh.ps1 -Host <pi-ip> -User <username> -Port 5000 -Platform linux/arm/v7`
   - Add `-NoCache` to force a clean Docker build when the UI/assets look stale.
5. Open the sender UI at `http://<pi-ip>:8080` (host networking). If you enable HTTPS (see below), use `https://<pi-ip>:8443`. The receiver listens on port 5000.
6. If `/sys/class/udc` is empty after boot, `dwc_otg` still owns the controller; verify the cmdline and reboot.

Why 32-bit? The Docker images in this repo are built for `linux/arm/v7`, which maps cleanly to 32-bit Raspberry Pi OS. If you prefer 64-bit Raspberry Pi OS, rebuild with `-Platform linux/arm64` and use arm64 base images for the Dockerfiles.

### Pi Zero (armv6) deployment (Python fallback)

- .NET 9/ASP.NET Core do not support armv6. Use the Python-based receiver/sender under `pi-zero/` on a Pi Zero.
- Setup:
  1. Fast path: on Windows run `pi-zero/install-to-sd.ps1 -BootDrive E: -Port 5000`, then on the Pi run `sudo /boot/install-linuxkey-pizero.sh`. This installs Python deps system-wide, sets up HID, and enables a systemd service for the Python receiver.
  2. Manual: copy `pi-zero/` and `PiSetup/setup-hid-gadget.sh` to the Pi, then `sudo apt-get update && sudo apt-get install -y python3 python3-pip`.
  3. Optional (for web UI): `pip3 install -r pi-zero/requirements-pizero.txt` (install system-wide so the service can use Flask).
  4. Run HID setup: `sudo bash /path/to/setup-hid-gadget.sh`.
  5. Start receiver: `cd /path/to/pi-zero && sudo python3 receiver.py 5000 --hid-path=/dev/hidg0`.
  6. Send text:
     - CLI: `python3 sender.py cli 127.0.0.1 5000 "Hello world!"`
     - Web (if Flask installed): `python3 sender.py serve 127.0.0.1 5000 --web-port 8080 --token yourtoken` (binds to 127.0.0.1 by default; add `--host 0.0.0.0` only if you need LAN access and protect it with a token). Then open `http://<pi-ip>:8080`.
  7. If the Pi is already booted and reachable, you can enable the USB gadget stack over SSH with `pi-zero/init-over-ssh.ps1 -Host <pi-ip> -User <username> -HidSetupPath /boot/linuxkey/setup-hid-gadget.sh -ForceDwc2`.

### Docker deployment

- Build and copy the full stack (receiver + sender) to an SD card on Windows: `.\docker-build-to-sd.ps1 -BootDrive E: -Port 5000 -Platform linux/arm/v7`. This writes two image tars, a compose file, and `/boot/install-linuxkey-docker.sh`; on the Pi run `sudo /boot/install-linuxkey-docker.sh` to load images and bring up the compose stack (requires Docker and the compose plugin on the Pi).
- Build and deploy over SSH: `.\deploy-docker-ssh.ps1 -Host 192.168.50.10 -User pi -Port 5000 -Platform linux/arm/v7`. This builds both images locally, copies them plus the compose file and HID setup script, then loads and runs the compose stack remotely (requires `ssh`/`scp` clients). Add `-NoCache` to force a clean rebuild. Add `-SkipUpload` if the `/tmp/linuxkey-*.tar`, `/tmp/docker-compose.yml`, and `/tmp/setup-hid-gadget.sh` files already exist on the Pi (`-SkipUpload` implies `-SkipBuild`).
- Compose stack runs on host networking; the sender UI is at `http://<pi-ip>:8080` and targets the receiver on `127.0.0.1:5000` by default. If HTTPS is enabled, it listens on `https://<pi-ip>:8443` and HTTP is disabled.
- Note: .NET containers require armv7+; Pi Zero (armv6) cannot run these images. Use a Zero 2 / Pi 3+ or deploy directly without Docker on armv6 hardware.
- Windows note: ensure Docker Desktop is running in **Linux containers** mode before running `deploy-docker-ssh.ps1`.

### HTTPS (Kestrel, optional)

The sender UI can run with HTTPS if you provide a PFX certificate. When HTTPS is enabled, HTTP is disabled.

- Local dev (Windows):
  - `dotnet dev-certs https -ep .\certs\sender.pfx -p "changeit"`
  - `setx SENDER_HTTPS_CERT_PATH C:\path\to\sender.pfx`
  - `setx SENDER_HTTPS_CERT_PASSWORD changeit`
  - `setx SENDER_HTTPS_PORT 8443`
  - Restart the app and open `https://localhost:8443`.

- Docker deployment (Pi):
  - `.\deploy-docker-ssh.ps1 -Host <pi-ip> -User <user> -Port 5000 -Platform linux/arm/v7 -HttpsCertPath C:\path\to\sender.pfx -HttpsCertPassword changeit -HttpsPort 8443`
  - Or for SD card: `.\docker-build-to-sd.ps1 -BootDrive E: -Port 5000 -Platform linux/arm/v7 -HttpsCertPath C:\path\to\sender.pfx -HttpsCertPassword changeit -HttpsPort 8443`
  - Open `https://<pi-ip>:8443`.

## Usage

1. Start the receiver on the Pi (with the HID gadget active).
2. Launch the sender web UI with `dotnet run --project SenderApp -- 192.168.50.10 5000` and open the logged URL (defaults to `http://localhost:8080`), or send a single payload with `dotnet run --project SenderApp -- 192.168.50.10 5000 "Hello world!"`.
3. When using the web UI, paste your text, press **Send**, and the text will be replayed as keystrokes on the USB-connected host.
4. The sender UI shows Raspberry Pi USB + Caps Lock status (dots in the top right). Hover the dots for connection details (target, last attempt, suggestions). Status updates via WebSocket push.
5. The Caps Lock dot reflects the host LED state when available (receiver writes it to `/run/linuxkey/capslock`).
6. Use **Back**/**Forward** to navigate previously sent text (stored in the browser local history).
7. The sender UI lets you pick the keyboard layout (en/de). When `SENDER_LAYOUT_TOKEN=true`, it sends a `{LAYOUT=..}` token before your text so the receiver can map correctly.
8. Settings and Keyboard panels are collapsible; chunk size is measured in **characters** (not bytes).
9. Sent status counts **characters from the textbox** (layout tokens added by the sender are excluded).
10. On smaller screens the sender UI switches to a mobile layout and hides the desktop-only function keys.

### Special keys

You can embed special key tokens in the text you send. Tokens are case-insensitive and wrapped in braces:

- `{BACKSPACE}`, `{ENTER}`, `{TAB}`, `{ESC}`
- `{DEL}`, `{DELETE}`, `{UP}`, `{DOWN}`, `{LEFT}`, `{RIGHT}`, `{HOME}`, `{END}`, `{PAGEUP}`, `{PAGEDOWN}`
- `{F1}` ... `{F12}`, `{PRINT}`, `{SCROLLLOCK}`, `{PAUSE}`, `{INSERT}`
- `{WIN}`, `{CTRL}`, `{ALT}`, `{SHIFT}` (modifier-only keys)

Use `{{` and `}}` to send literal `{` or `}` characters.

Aliases: `{BKSP}`, `{PRTSC}`, `{PRINTSCREEN}`, `{SCRLK}`, `{BREAK}`, `{INS}`.

Note: `{LAYOUT=..}` tokens are only understood by the .NET receiver. If you use the Python receiver under `pi-zero/`, disable layout prefixing with `SENDER_LAYOUT_TOKEN=false`.

For key chords, use `+` inside a token, for example:

- `{CTRL+C}`, `{CTRL+ALT+DEL}`, `{ALT+TAB}`
- `{SHIFT+WIN+S}` (modifiers + key)

### Keyboard layout (en/de)

The receiver can target different host keyboard layouts:

- `--layout=en` (default, US QWERTY)
- `--layout=de` (German QWERTZ)

You can also set `RECEIVER_LAYOUT=en|de` as an environment variable.

## Testing & coverage

Run the receiver test suite:

```bash
dotnet test ReceiverApp.Tests/ReceiverApp.Tests.fsproj
```

Run the sender test suite:

```bash
dotnet test SenderApp.Tests/SenderApp.Tests.fsproj
```

Collect coverage at the same time with:

```bash
dotnet test --settings coverage.runsettings --collect:"XPlat Code Coverage"
```

Coverage reports (Cobertura, lcov, OpenCover) are written underneath each test project's `TestResults/<run-id>/` folder (for example `SenderApp.Tests/TestResults/<run-id>/`). The latest sender run produced about **56.44% line** and **48.49% branch** coverage. Use tools such as [`reportgenerator`](https://github.com/danielpalme/ReportGenerator) to turn the XML output into HTML summaries, for example:

```bash
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator "-reports:SenderApp.Tests/TestResults/*/coverage.opencover.xml" "-targetdir:coverage-report"
```
