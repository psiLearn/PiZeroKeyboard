param(
    # Drive letter of the Raspberry Pi boot partition (e.g., "E:")
    [string]$BootDrive = "E:",
    # TCP port the receiver should listen on
    [int]$Port = 5000,
    # Docker image tags to embed
    [string]$ReceiverImage = "linuxkey-receiver:local",
    [string]$SenderImage = "linuxkey-sender:local",
    # Build platform (Pi Zero often needs arm/v7 images; adjust if required)
    [string]$Platform = "linux/arm/v7"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker CLI not found. Install Docker Desktop/Engine with buildx support."
}

$bootRoot = "$BootDrive\"
if (-not (Test-Path $bootRoot)) {
    throw "Boot drive '$BootDrive' not found. Insert the SD card and set -BootDrive accordingly."
}

$configProbe = Join-Path $bootRoot "config.txt"
if (-not (Test-Path $configProbe)) {
    Write-Warning "No config.txt found at $configProbe. Ensure -BootDrive points to the Pi boot partition."
}

$tarPath = Join-Path $PSScriptRoot "linuxkey-receiver.tar"
$senderTar = Join-Path $PSScriptRoot "linuxkey-sender.tar"
foreach ($p in @($tarPath, $senderTar)) { if (Test-Path $p) { Remove-Item $p -Force } }

Write-Host "Building receiver image ($Platform) -> $tarPath" -ForegroundColor Cyan
docker buildx build `
    --platform $Platform `
    -f (Join-Path $PSScriptRoot "Dockerfile.receiver") `
    -t $ReceiverImage `
    --load `
    $PSScriptRoot
docker save -o $tarPath $ReceiverImage

Write-Host "Building sender image ($Platform) -> $senderTar" -ForegroundColor Cyan
docker buildx build `
    --platform $Platform `
    -f (Join-Path $PSScriptRoot "Dockerfile.sender") `
    -t $SenderImage `
    --load `
    $PSScriptRoot
docker save -o $senderTar $SenderImage

foreach ($p in @($tarPath, $senderTar)) {
    if (-not (Test-Path $p)) { throw "Build did not produce $p" }
}

$targetDir = Join-Path $bootRoot "linuxkey"
New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

Write-Host "Copying image tar and setup script to $targetDir" -ForegroundColor Cyan
Copy-Item -Path $tarPath -Destination (Join-Path $targetDir "linuxkey-receiver.tar") -Force
Copy-Item -Path $senderTar -Destination (Join-Path $targetDir "linuxkey-sender.tar") -Force
Copy-Item -Path (Join-Path $PSScriptRoot "PiSetup/setup-hid-gadget.sh") -Destination (Join-Path $targetDir "setup-hid-gadget.sh") -Force

$composeContent = @"
version: "3.9"
services:
  receiver:
    image: $ReceiverImage
    privileged: true
    network_mode: host
    environment:
      - RECEIVER_LAYOUT=en
    devices:
      - /dev/hidg0:/dev/hidg0
    command: ["$Port"]
    restart: unless-stopped

  sender:
    image: $SenderImage
    network_mode: host
    depends_on:
      - receiver
    restart: unless-stopped
    user: "1000:1000"
    environment:
      - SENDER_TARGET_IP=127.0.0.1
      - SENDER_TARGET_PORT=$Port
      - SENDER_WEB_PORT=8080
      - SENDER_LAYOUT_TOKEN=true
"@
Set-Content -Path (Join-Path $targetDir "docker-compose.yml") -Value $composeContent -Encoding ASCII

$installScript = @'
#!/bin/bash
set -euo pipefail

arch=$(uname -m)
if [ "$arch" = "armv6l" ]; then
    echo "armv6l detected: .NET container images do not support Pi Zero (armv6). Use a Pi with armv7+ or deploy without Docker." >&2
    exit 1
fi

sudo apt-get update
if ! command -v docker >/dev/null 2>&1; then
    sudo apt-get install -y docker.io
fi

compose_cmd=()
if docker compose version >/dev/null 2>&1; then
  compose_cmd=(sudo docker compose)
elif command -v docker-compose >/dev/null 2>&1; then
  compose_cmd=(sudo docker-compose)
else
  sudo apt-get install -y docker-compose-plugin || sudo apt-get install -y docker-compose
  if docker compose version >/dev/null 2>&1; then
    compose_cmd=(sudo docker compose)
  elif command -v docker-compose >/dev/null 2>&1; then
    compose_cmd=(sudo docker-compose)
  else
    echo "Docker Compose not available." >&2
    exit 1
  fi
fi

sudo modprobe libcomposite || true
if ! mountpoint -q /sys/kernel/config; then
  sudo mount -t configfs none /sys/kernel/config
fi

cat <<'EOF' | sudo tee /etc/systemd/system/linuxkey-hid-gadget.service >/dev/null
[Unit]
Description=LinuxKey USB HID gadget setup
After=systemd-modules-load.service
Wants=systemd-modules-load.service
ConditionPathExists=/boot/linuxkey/setup-hid-gadget.sh

[Service]
Type=oneshot
ExecStart=/bin/bash /boot/linuxkey/setup-hid-gadget.sh
RemainAfterExit=yes

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable linuxkey-hid-gadget.service
sudo bash /boot/linuxkey/setup-hid-gadget.sh
sudo docker load -i /boot/linuxkey/linuxkey-receiver.tar
sudo docker load -i /boot/linuxkey/linuxkey-sender.tar
sudo docker rm -f linuxkey-receiver || true
sudo docker rm -f linuxkey-sender || true

"${compose_cmd[@]}" -f /boot/linuxkey/docker-compose.yml up -d --force-recreate
echo "LinuxKey stack deployed (receiver + sender)."
'@


$installPath = Join-Path $bootRoot "install-linuxkey-docker.sh"
Set-Content -Path $installPath -Value $installScript -Encoding ASCII

Write-Host "Done. On the Pi, run: sudo /boot/install-linuxkey-docker.sh" -ForegroundColor Green
