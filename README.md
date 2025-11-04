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
