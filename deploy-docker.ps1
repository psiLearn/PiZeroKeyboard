param(
    # Hostname or IP of the Raspberry Pi (SSH reachable)
    [Parameter(Mandatory = $true)]
    [Alias("Host")]
    [string]$TargetHost,
    [string]$User = "pi",
    [int]$SshPort = 22,
    [int]$Port = 5000,
    [string]$ReceiverImage = "linuxkey-receiver:local",
    [string]$SenderImage = "linuxkey-sender:local",
    [string]$Platform = "linux/arm/v7",
    [int]$RetryCount = 3,
    [int]$RetryDelaySec = 5,
    [int]$SshTimeoutSec = 15,
    [switch]$CompressTransfers,
    [switch]$SkipBuild,
    [switch]$SkipUpload
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw "Docker CLI not found." }
if (-not (Get-Command ssh -ErrorAction SilentlyContinue)) { throw "ssh command not found." }
if (-not (Get-Command scp -ErrorAction SilentlyContinue)) { throw "scp command not found." }

$sshTarget = "${User}@${TargetHost}"
$sshOptions = @(
    "-o", "ServerAliveInterval=30",
    "-o", "ServerAliveCountMax=5",
    "-o", "TCPKeepAlive=yes",
    "-o", "ConnectTimeout=$SshTimeoutSec",
    "-o", "ConnectionAttempts=3",
    "-o", "StrictHostKeyChecking=accept-new"
)

function Invoke-ScpWithRetry {
    param(
        [string]$Source,
        [string]$Destination,
        [string]$Purpose
    )

    for ($attempt = 1; $attempt -le $RetryCount; $attempt++) {
        Write-Host "SCP $Purpose (attempt $attempt/$RetryCount)..." -ForegroundColor Cyan
        $scpArgs = @()
        if ($CompressTransfers) { $scpArgs += "-C" }
        $scpArgs += @($Source, $Destination)
        & scp @sshOptions -P $SshPort @scpArgs
        if ($LASTEXITCODE -eq 0) { return }
        if ($attempt -lt $RetryCount) {
            Write-Warning "SCP failed. Retrying in $RetryDelaySec seconds..."
            Start-Sleep -Seconds $RetryDelaySec
        }
    }
    throw "SCP failed for $Purpose after $RetryCount attempts."
}

function Invoke-SshWithRetry {
    param(
        [string]$Command,
        [string]$Purpose
    )

    for ($attempt = 1; $attempt -le $RetryCount; $attempt++) {
        Write-Host "SSH $Purpose (attempt $attempt/$RetryCount)..." -ForegroundColor Cyan
        & ssh @sshOptions -p $SshPort $sshTarget $Command
        if ($LASTEXITCODE -eq 0) { return }
        if ($attempt -lt $RetryCount) {
            Write-Warning "SSH failed. Retrying in $RetryDelaySec seconds..."
            Start-Sleep -Seconds $RetryDelaySec
        }
    }
    throw "SSH failed for $Purpose after $RetryCount attempts."
}

$receiverTar = Join-Path $PSScriptRoot "linuxkey-receiver.tar"
$senderTar = Join-Path $PSScriptRoot "linuxkey-sender.tar"
if (-not $SkipBuild) {
    foreach ($p in @($receiverTar, $senderTar)) {
        if (Test-Path $p) { Remove-Item $p -Force }
    }
}

if (-not $SkipBuild) {
    Write-Host "Building receiver image ($Platform) -> $receiverTar" -ForegroundColor Cyan
    docker buildx build --platform $Platform -f (Join-Path $PSScriptRoot "Dockerfile.receiver") -t $ReceiverImage --load $PSScriptRoot
    docker save -o $receiverTar $ReceiverImage
}

if (-not $SkipBuild) {
    Write-Host "Building sender image ($Platform) -> $senderTar" -ForegroundColor Cyan
    docker buildx build --platform $Platform -f (Join-Path $PSScriptRoot "Dockerfile.sender") -t $SenderImage --load $PSScriptRoot
    docker save -o $senderTar $SenderImage
}

if (-not $SkipBuild) {
    foreach ($p in @($receiverTar, $senderTar)) { if (-not (Test-Path $p)) { throw "Build did not produce $p" } }
}

$composeContent = @"
version: "3.9"
services:
  receiver:
    image: ${ReceiverImage}
    privileged: true
    network_mode: host
    environment:
      - RECEIVER_LAYOUT=en
    devices:
      - /dev/hidg0:/dev/hidg0
    command: ["${Port}"]
    restart: unless-stopped

  sender:
    image: ${SenderImage}
    network_mode: host
    depends_on:
      - receiver
    restart: unless-stopped
    user: "1000:1000"
    environment:
      - SENDER_TARGET_IP=127.0.0.1
      - SENDER_TARGET_PORT=${Port}
      - SENDER_WEB_PORT=8080
      - SENDER_LAYOUT_TOKEN=true
"@

$composeTemp = Join-Path ([System.IO.Path]::GetTempPath()) ("linuxkey-compose-" + [System.Guid]::NewGuid().ToString("N") + ".yml")
Set-Content -Path $composeTemp -Value $composeContent -Encoding ASCII

Write-Host "Copying artifacts to $TargetHost..." -ForegroundColor Cyan
if ($SkipUpload) {
    Write-Warning "Skipping uploads; expecting /tmp/linuxkey-*.tar, /tmp/docker-compose.yml, and /tmp/setup-hid-gadget.sh to exist on the target."
} else {
    if ($SkipBuild) {
        foreach ($p in @($receiverTar, $senderTar)) {
            if (-not (Test-Path $p)) { throw "Missing $p. Provide tar files or disable -SkipBuild." }
        }
    }
    Invoke-ScpWithRetry -Source $receiverTar -Destination "${sshTarget}:/tmp/linuxkey-receiver.tar" -Purpose "receiver image"
    Invoke-ScpWithRetry -Source $senderTar -Destination "${sshTarget}:/tmp/linuxkey-sender.tar" -Purpose "sender image"
    Invoke-ScpWithRetry -Source $composeTemp -Destination "${sshTarget}:/tmp/docker-compose.yml" -Purpose "compose file"
    Invoke-ScpWithRetry -Source (Join-Path $PSScriptRoot "PiSetup/setup-hid-gadget.sh") -Destination "${sshTarget}:/tmp/setup-hid-gadget.sh" -Purpose "HID setup script"
}

$remote = @'
set -e
arch=$(uname -m)
if [ "$arch" = "armv6l" ]; then
    echo "armv6l detected: .NET containers need armv7+." >&2
    exit 1
fi
sudo apt-get update
if ! command -v docker >/dev/null 2>&1; then
    sudo apt-get install -y docker.io
fi
compose_cmd=""
if docker compose version >/dev/null 2>&1; then
  compose_cmd="docker compose"
elif command -v docker-compose >/dev/null 2>&1; then
  compose_cmd="docker-compose"
else
  sudo apt-get install -y docker-compose-plugin || sudo apt-get install -y docker-compose
  if docker compose version >/dev/null 2>&1; then
    compose_cmd="docker compose"
  elif command -v docker-compose >/dev/null 2>&1; then
    compose_cmd="docker-compose"
  else
    echo "Docker Compose not available." >&2
    exit 1
  fi
fi
sudo modprobe libcomposite || true
if ! mountpoint -q /sys/kernel/config; then
  sudo mount -t configfs none /sys/kernel/config
fi
hid_path="/tmp/setup-hid-gadget.sh"
if [ -f /tmp/setup-hid-gadget.sh ]; then
  sudo mkdir -p /boot/linuxkey
  sudo mv /tmp/setup-hid-gadget.sh /boot/linuxkey/setup-hid-gadget.sh
  sudo chmod +x /boot/linuxkey/setup-hid-gadget.sh
  hid_path="/boot/linuxkey/setup-hid-gadget.sh"
elif [ -f /boot/linuxkey/setup-hid-gadget.sh ]; then
  hid_path="/boot/linuxkey/setup-hid-gadget.sh"
fi
if [ ! -f "$hid_path" ]; then
  echo "HID setup script not found." >&2
  exit 1
fi
cat <<EOF | sudo tee /etc/systemd/system/linuxkey-hid-gadget.service >/dev/null
[Unit]
Description=LinuxKey USB HID gadget setup
After=systemd-modules-load.service
Wants=systemd-modules-load.service
ConditionPathExists=$hid_path

[Service]
Type=oneshot
ExecStart=/bin/bash $hid_path
RemainAfterExit=yes

[Install]
WantedBy=multi-user.target
EOF
sudo systemctl daemon-reload
sudo systemctl enable linuxkey-hid-gadget.service
sudo bash "$hid_path"
sudo docker load -i /tmp/linuxkey-receiver.tar
sudo docker load -i /tmp/linuxkey-sender.tar
sudo docker rm -f linuxkey-receiver || true
sudo docker rm -f linuxkey-sender || true
sudo $compose_cmd -f /tmp/docker-compose.yml up -d --force-recreate
'@

Write-Host "Deploying via SSH..." -ForegroundColor Cyan
Invoke-SshWithRetry -Command $remote -Purpose "deploy"

Remove-Item $composeTemp -ErrorAction SilentlyContinue
Write-Host "Done. Stack should be running on $TargetHost." -ForegroundColor Green
