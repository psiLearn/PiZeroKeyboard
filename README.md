# LinuxKey

Windows sender and Raspberry Pi Zero receiver written in F# for transmitting text over TCP and replaying it as USB HID keyboard events.

## Projects

- `SenderApp` – Windows console app that sends UTF-8 text to the Pi. Run with `dotnet run -- <pi-ip> <port> "text"`.
- `ReceiverApp` – Raspberry Pi console app that listens for TCP connections, converts characters to HID codes, and writes them to `/dev/hidg0`.
- `PiSetup/setup-hid-gadget.sh` – Helper script that configures the Pi Zero USB gadget as a keyboard (run once per boot as root).

## Build

```bash
dotnet build SenderApp
dotnet build ReceiverApp
```

## Deploy on Raspberry Pi

1. Ensure OTG mode is enabled in `/boot/config.txt` and `/boot/cmdline.txt` by loading the `dwc2` driver.
2. Run `sudo /opt/hid/setup-hid-gadget.sh` after copying the script to the Pi to expose `/dev/hidg0`.
3. Publish the receiver with `dotnet publish ReceiverApp -c Release -r linux-arm --self-contained false` and run it with `sudo dotnet ReceiverApp.dll 5000`.

### Pi Zero (armv6) deployment (Python fallback)

- .NET 9/ASP.NET Core do not support armv6. Use the Python-based receiver/sender under `pi-zero/` on a Pi Zero.
- Setup:
  1. Copy `pi-zero/` and `PiSetup/setup-hid-gadget.sh` to the Pi (or use `pi-zero/install-to-sd.ps1 -BootDrive E: -Port 5000` on Windows to prepare the SD card with an install script).
  2. On the Pi: `sudo apt-get update && sudo apt-get install -y python3 python3-pip`.
  3. Optional (for web UI): `pip3 install --user -r pi-zero/requirements-pizero.txt`.
  4. Run HID setup: `sudo bash /path/to/setup-hid-gadget.sh`.
  5. Start receiver: `cd /path/to/pi-zero && sudo python3 receiver.py 5000 --hid-path=/dev/hidg0`.
  6. Send text:
     - CLI: `python3 sender.py cli 127.0.0.1 5000 "Hello world!"`
     - Web (if Flask installed): `python3 sender.py serve 127.0.0.1 5000 --web-port 8080 --token yourtoken` (binds to 127.0.0.1 by default; add `--host 0.0.0.0` only if you need LAN access and protect it with a token). Then open `http://<pi-ip>:8080`.

### Docker deployment

- Build and copy the full stack (receiver + sender) to an SD card on Windows: `.\docker-build-to-sd.ps1 -BootDrive E: -Port 5000 -Platform linux/arm/v7`. This writes two image tars, a compose file, and `/boot/install-linuxkey-docker.sh`; on the Pi run `sudo /boot/install-linuxkey-docker.sh` to load images and bring up the compose stack (requires Docker and the compose plugin on the Pi).
- Build and deploy over SSH: `.\deploy-docker-ssh.ps1 -Host 192.168.50.10 -User pi -Port 5000 -Platform linux/arm/v7`. This builds both images locally, copies them plus the compose file and HID setup script, then loads and runs the compose stack remotely (requires `ssh`/`scp` clients).
- Compose stack runs on host networking; the sender UI is at `http://<pi-ip>:8080` and targets the receiver on `127.0.0.1:5000` by default.
- Note: .NET containers require armv7+; Pi Zero (armv6) cannot run these images. Use a Zero 2 / Pi 3+ or deploy directly without Docker on armv6 hardware.

## Usage

1. Start the receiver on the Pi (with the HID gadget active).
2. Launch the sender web UI with `dotnet run --project SenderApp -- 192.168.50.10 5000` and open the logged URL (defaults to `http://localhost:8080`), or send a single payload with `dotnet run --project SenderApp -- 192.168.50.10 5000 "Hello world!"`.
3. When using the web UI, paste your text, press **Send**, and the text will be replayed as keystrokes on the USB-connected host.

## Testing & coverage

Run the receiver test suite:

```bash
dotnet test ReceiverApp.Tests/ReceiverApp.Tests.fsproj
```

Collect coverage at the same time with:

```bash
dotnet test --settings coverage.runsettings --collect:"XPlat Code Coverage"
```

Coverage reports (Cobertura, lcov, OpenCover) are written underneath `ReceiverApp.Tests/TestResults/<run-id>/`. The latest run produced about **62 % line** and **47 % branch** coverage, with gaps concentrated in `ReceiverApp/Program.fs`. Use tools such as [`reportgenerator`](https://github.com/danielpalme/ReportGenerator) to turn the XML output into HTML summaries, for example:

```bash
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator "-reports:ReceiverApp.Tests/TestResults/*/coverage.opencover.xml" "-targetdir:coverage-report"
```
