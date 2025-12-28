param(
    # Drive letter of the Raspberry Pi boot partition (e.g., "E:")
    [string]$BootDrive = "E:",
    # TCP port for the receiver
    [int]$Port = 5000
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$bootRoot = "$BootDrive\"
if (-not (Test-Path $bootRoot)) {
    throw "Boot drive '$BootDrive' not found. Insert the SD card and set -BootDrive accordingly."
}

$configProbe = Join-Path $bootRoot "config.txt"
if (-not (Test-Path $configProbe)) {
    Write-Warning "No config.txt found at $configProbe. Ensure -BootDrive points to the Pi boot partition."
}

$targetDir = Join-Path $bootRoot "linuxkey"
New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

Write-Host "Copying Pi Zero Python receiver/sender and HID setup script to $targetDir" -ForegroundColor Cyan
Copy-Item -Path (Join-Path $PSScriptRoot "pi-zero/*") -Destination $targetDir -Recurse -Force
Copy-Item -Path (Join-Path $PSScriptRoot "PiSetup/setup-hid-gadget.sh") -Destination $targetDir -Force

$installScript = @"
#!/bin/bash
set -euo pipefail

sudo apt-get update
sudo apt-get install -y python3 python3-pip

sudo bash /boot/linuxkey/setup-hid-gadget.sh

pip3 install -r /boot/linuxkey/requirements-pizero.txt || true

cat <<'EOF' | sudo tee /etc/systemd/system/linuxkey-receiver.service >/dev/null
[Unit]
Description=LinuxKey Python receiver (Pi Zero)
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
ExecStart=/usr/bin/python3 /boot/linuxkey/receiver.py $Port --hid-path=/dev/hidg0
Restart=always
RestartSec=3
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable --now linuxkey-receiver.service

echo "LinuxKey Python receiver installed and started (port $Port)."
echo "Sender CLI: python3 /boot/linuxkey/sender.py cli <ip> $Port \"text\""
echo "Sender UI (if Flask installed): python3 /boot/linuxkey/sender.py serve <ip> $Port --host 0.0.0.0 --web-port 8080"
"@

$installPath = Join-Path $bootRoot "install-linuxkey-pizero.sh"
Set-Content -Path $installPath -Value $installScript -Encoding ASCII

Write-Host "Done. On the Pi Zero, run: sudo /boot/install-linuxkey-pizero.sh" -ForegroundColor Green
