param(
    # Hostname or IP of the Raspberry Pi Zero
    [Parameter(Mandatory = $true)]
    [string]$Host,
    # SSH username
    [string]$User = "pi",
    # SSH port
    [int]$SshPort = 22,
    # TCP port for the receiver
    [int]$Port = 5000,
    # Docker image tags
    [string]$ReceiverImage = "linuxkey-receiver:local",
    [string]$SenderImage = "linuxkey-sender:local",
    # Build platform
    [string]$Platform = "linux/arm/v7"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker CLI not found. Install Docker Desktop/Engine with buildx support."
}
if (-not (Get-Command ssh -ErrorAction SilentlyContinue)) {
    throw "ssh command not found. Install OpenSSH client."
}
if (-not (Get-Command scp -ErrorAction SilentlyContinue)) {
    throw "scp command not found. Install OpenSSH client."
}

$tarPath = Join-Path $PSScriptRoot "linuxkey-receiver.tar"
$senderTar = Join-Path $PSScriptRoot "linuxkey-sender.tar"
foreach ($p in @($tarPath, $senderTar)) { if (Test-Path $p) { Remove-Item $p -Force } }

Write-Host "Building receiver image ($Platform) -> $tarPath" -ForegroundColor Cyan
docker buildx build `
    --platform $Platform `
    -f (Join-Path $PSScriptRoot "Dockerfile.receiver") `
    -t $ReceiverImage `
    --output type=docker,dest=$tarPath `
    $PSScriptRoot

Write-Host "Building sender image ($Platform) -> $senderTar" -ForegroundColor Cyan
docker buildx build `
    --platform $Platform `
    -f (Join-Path $PSScriptRoot "Dockerfile.sender") `
    -t $SenderImage `
    --output type=docker,dest=$senderTar `
    $PSScriptRoot

foreach ($p in @($tarPath, $senderTar)) {
    if (-not (Test-Path $p)) { throw "Build did not produce $p" }
}

$composeTemp = Join-Path ([System.IO.Path]::GetTempPath()) ("linuxkey-compose-" + [System.Guid]::NewGuid().ToString("N") + ".yml")
$composeContent = @"
version: "3.9"
services:
  receiver:
    image: $ReceiverImage
    privileged: true
    network_mode: host
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
"@
Set-Content -Path $composeTemp -Value $composeContent -Encoding ASCII

Write-Host "Copying artifacts to $Host..." -ForegroundColor Cyan
& scp -P $SshPort $tarPath "$User@$Host:/tmp/linuxkey-receiver.tar"
& scp -P $SshPort $senderTar "$User@$Host:/tmp/linuxkey-sender.tar"
& scp -P $SshPort (Join-Path $PSScriptRoot "PiSetup/setup-hid-gadget.sh") "$User@$Host:/tmp/setup-hid-gadget.sh"
& scp -P $SshPort $composeTemp "$User@$Host:/tmp/docker-compose.yml"

$remote = @"
set -e
arch=\$(uname -m)
if [ "\$arch" = "armv6l" ]; then
    echo "armv6l detected: .NET container images do not support Pi Zero (armv6). Use a Pi with armv7+ or deploy without Docker." >&2
    exit 1
fi
sudo apt-get update
if ! command -v docker >/dev/null 2>&1; then
    sudo apt-get install -y docker.io
fi
sudo apt-get install -y docker-compose-plugin
sudo bash /tmp/setup-hid-gadget.sh
sudo docker load -i /tmp/linuxkey-receiver.tar
sudo docker load -i /tmp/linuxkey-sender.tar
sudo docker rm -f linuxkey-receiver || true
sudo docker rm -f linuxkey-sender || true
sudo docker compose -f /tmp/docker-compose.yml up -d
"@

Write-Host "Deploying via SSH..." -ForegroundColor Cyan
ssh -p $SshPort "$User@$Host" "$remote"

Remove-Item $composeTemp -ErrorAction SilentlyContinue

Write-Host "Done. Container 'linuxkey-receiver' should be running on $Host (port $Port)." -ForegroundColor Green
