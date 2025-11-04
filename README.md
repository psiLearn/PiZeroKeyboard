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
2. Execute `dotnet run -- 192.168.50.10 5000 "Hello world!"` from `SenderApp` on Windows.
3. The text appears as keystrokes on the host connected to the Pi via USB.
