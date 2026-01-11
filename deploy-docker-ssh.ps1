param(
    # Hostname or IP of the Raspberry Pi Zero
    [Parameter(Mandatory = $true)]
    [Alias("Host")]
    [string]$TargetHost,
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

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker CLI not found. Install Docker Desktop/Engine with buildx support."
}
if (-not (Get-Command ssh -ErrorAction SilentlyContinue)) {
    throw "ssh command not found. Install OpenSSH client."
}
if (-not (Get-Command scp -ErrorAction SilentlyContinue)) {
    throw "scp command not found. Install OpenSSH client."
}

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

$tarPath = Join-Path $PSScriptRoot "linuxkey-receiver.tar"
$senderTar = Join-Path $PSScriptRoot "linuxkey-sender.tar"
if (-not $SkipBuild) {
    foreach ($p in @($tarPath, $senderTar)) {
        if (Test-Path $p) { Remove-Item $p -Force }
    }
}

if (-not $SkipBuild) {
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
}

if (-not $SkipBuild) {
    foreach ($p in @($tarPath, $senderTar)) {
        if (-not (Test-Path $p)) { throw "Build did not produce $p" }
    }
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

Write-Host "Copying artifacts to $TargetHost..." -ForegroundColor Cyan
if ($SkipUpload) {
    Write-Warning "Skipping uploads; expecting /tmp/linuxkey-*.tar, /tmp/docker-compose.yml, and /tmp/setup-hid-gadget.sh to exist on the target."
} else {
    if ($SkipBuild) {
        foreach ($p in @($tarPath, $senderTar)) {
            if (-not (Test-Path $p)) { throw "Missing $p. Provide tar files or disable -SkipBuild." }
        }
    }
    Invoke-ScpWithRetry -Source $tarPath -Destination "${sshTarget}:/tmp/linuxkey-receiver.tar" -Purpose "receiver image"
    Invoke-ScpWithRetry -Source $senderTar -Destination "${sshTarget}:/tmp/linuxkey-sender.tar" -Purpose "sender image"
    Invoke-ScpWithRetry -Source (Join-Path $PSScriptRoot "PiSetup/setup-hid-gadget.sh") -Destination "${sshTarget}:/tmp/setup-hid-gadget.sh" -Purpose "HID setup script"
    Invoke-ScpWithRetry -Source $composeTemp -Destination "${sshTarget}:/tmp/docker-compose.yml" -Purpose "compose file"
}

$remote = @'
set -e
arch=$(uname -m)
if [ "$arch" = "armv6l" ]; then
    echo "armv6l detected: .NET container images do not support Pi Zero (armv6). Use a Pi with armv7+ or deploy without Docker." >&2
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
sudo bash /tmp/setup-hid-gadget.sh
sudo docker load -i /tmp/linuxkey-receiver.tar
sudo docker load -i /tmp/linuxkey-sender.tar
sudo docker rm -f linuxkey-receiver || true
sudo docker rm -f linuxkey-sender || true
sudo $compose_cmd -f /tmp/docker-compose.yml up -d
'@

Write-Host "Deploying via SSH..." -ForegroundColor Cyan
Invoke-SshWithRetry -Command $remote -Purpose "deploy"

Remove-Item $composeTemp -ErrorAction SilentlyContinue

Write-Host "Done. Container 'linuxkey-receiver' should be running on $TargetHost (port $Port)." -ForegroundColor Green
