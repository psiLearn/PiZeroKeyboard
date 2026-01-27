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
    # HTTPS settings for the sender UI (optional)
    [string]$HttpsCertPath = "",
    [string]$HttpsCertPassword = "",
    [int]$HttpsPort = 8443,
    [int]$RetryCount = 3,
    [int]$RetryDelaySec = 5,
    [int]$SshTimeoutSec = 15,
    [switch]$CompressTransfers,
    [switch]$SkipBuild,
    [switch]$SkipUpload,
    [switch]$NoCache
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

$isWindowsHost =
    ($PSVersionTable.PSEdition -eq "Desktop") -or
    ($env:OS -eq "Windows_NT") -or
    ([System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT)

function Get-DockerOsType {
    $osType = & docker info --format "{{.OSType}}" 2>$null
    if ($LASTEXITCODE -ne 0) { return $null }
    if ([string]::IsNullOrWhiteSpace($osType)) { return $null }
    return $osType.Trim()
}

function Start-DockerDesktopIfAvailable {
    if (-not $isWindowsHost) { return $false }
    $candidates = @(
        (Join-Path $env:ProgramFiles "Docker\Docker\Docker Desktop.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Docker\Docker\Docker Desktop.exe")
    ) | Where-Object { $_ -and (Test-Path $_) }
    foreach ($path in $candidates) {
        try {
            Start-Process -FilePath $path | Out-Null
            return $true
        } catch {
            # ignore and continue
        }
    }
    return $false
}

function Ensure-DockerReady {
    param(
        [int]$Attempts = 6,
        [int]$DelaySec = 5
    )

    for ($i = 1; $i -le $Attempts; $i++) {
        $osType = Get-DockerOsType
        if ($osType) { return $osType }
        if ($i -eq 1) {
            Start-DockerDesktopIfAvailable | Out-Null
        }
        Start-Sleep -Seconds $DelaySec
    }
    throw "Docker engine is not reachable. Start Docker Desktop and ensure Linux containers are enabled, then retry."
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
$httpsEnabled = -not [string]::IsNullOrWhiteSpace($HttpsCertPath)
$remoteCertDir = "/etc/linuxkey/certs"
$remoteCertPath = "$remoteCertDir/sender.pfx"
$remoteCertTemp = "/tmp/linuxkey-sender.pfx"
if ($httpsEnabled -and -not (Test-Path $HttpsCertPath)) {
    throw "HTTPS certificate not found at '$HttpsCertPath'."
}
if (-not $SkipBuild) {
    $dockerOs = Ensure-DockerReady
    if ($dockerOs -ne "linux") {
        throw "Docker engine is running in '$dockerOs' mode. Switch to Linux containers and retry."
    }
    foreach ($p in @($tarPath, $senderTar)) {
        if (Test-Path $p) { Remove-Item $p -Force }
    }
}

if (-not $SkipBuild) {
    Write-Host "Building receiver image ($Platform) -> $tarPath" -ForegroundColor Cyan
    $cacheArgs = @()
    if ($NoCache) { $cacheArgs += "--no-cache" }
    docker buildx build `
        --platform $Platform `
        -f (Join-Path $PSScriptRoot "Dockerfile.receiver") `
        -t $ReceiverImage `
        --load `
        @cacheArgs `
        $PSScriptRoot
    docker save -o $tarPath $ReceiverImage

    Write-Host "Building sender image ($Platform) -> $senderTar" -ForegroundColor Cyan
    docker buildx build `
        --platform $Platform `
        -f (Join-Path $PSScriptRoot "Dockerfile.sender") `
        -t $SenderImage `
        --load `
        @cacheArgs `
        $PSScriptRoot
    docker save -o $senderTar $SenderImage
}

if (-not $SkipBuild) {
    foreach ($p in @($tarPath, $senderTar)) {
        if (-not (Test-Path $p)) { throw "Build did not produce $p" }
    }
}

$composeTemp = Join-Path ([System.IO.Path]::GetTempPath()) ("linuxkey-compose-" + [System.Guid]::NewGuid().ToString("N") + ".yml")
$httpsEnvLines = ""
$httpsVolumeLine = ""
if ($httpsEnabled) {
    $httpsEnvLines = "      - SENDER_HTTPS_CERT_PATH=$remoteCertPath`n"
    if (-not [string]::IsNullOrWhiteSpace($HttpsCertPassword)) {
        $httpsEnvLines += "      - SENDER_HTTPS_CERT_PASSWORD=$HttpsCertPassword`n"
    }
    $httpsEnvLines += "      - SENDER_HTTPS_PORT=$HttpsPort`n"
    $httpsVolumeLine = "      - ${remoteCertDir}:${remoteCertDir}:ro`n"
}
$composeContent = @"
services:
  receiver:
    image: $ReceiverImage
    privileged: true
    network_mode: host
    environment:
      - RECEIVER_LAYOUT=en
      - RECEIVER_CAPSLOCK_PATH=/run/linuxkey/capslock
    devices:
      - /dev/hidg0:/dev/hidg0
    volumes:
      - /run/linuxkey:/run/linuxkey
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
      - SENDER_USB_STATE_PATH=/sys/class/udc/3f980000.usb/state
      - SENDER_CAPSLOCK_PATH=/run/linuxkey/capslock
      - SENDER_LAYOUT_TOKEN=true
$httpsEnvLines    volumes:
      - /run/linuxkey:/run/linuxkey:ro
$httpsVolumeLine
"@
Set-Content -Path $composeTemp -Value $composeContent -Encoding ASCII

Write-Host "Copying artifacts to $TargetHost..." -ForegroundColor Cyan
if ($SkipUpload) {
    Write-Warning "Skipping uploads; expecting /tmp/linuxkey-*.tar, /tmp/docker-compose.yml, and /tmp/setup-hid-gadget.sh to exist on the target."
    if ($httpsEnabled) {
        Write-Warning "HTTPS enabled; ensure $remoteCertPath exists on the target."
    }
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
    if ($httpsEnabled) {
        Invoke-ScpWithRetry -Source $HttpsCertPath -Destination "${sshTarget}:${remoteCertTemp}" -Purpose "HTTPS certificate"
    }
}

$remoteTemplate = @'
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
cert_temp="__CERT_TEMP__"
cert_dir="__CERT_DIR__"
cert_dest="__CERT_DEST__"
if [ -f "$cert_temp" ]; then
  sudo mkdir -p "$cert_dir"
  sudo mv "$cert_temp" "$cert_dest"
  sudo chmod 644 "$cert_dest"
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
compose_project="linuxkey"
sudo $compose_cmd -p "$compose_project" -f /tmp/docker-compose.yml down --remove-orphans || true
sudo $compose_cmd -p "$compose_project" -f /tmp/docker-compose.yml up -d --force-recreate
'@

$remote = $remoteTemplate.Replace("__CERT_TEMP__", $remoteCertTemp).Replace("__CERT_DIR__", $remoteCertDir).Replace("__CERT_DEST__", $remoteCertPath)

Write-Host "Deploying via SSH..." -ForegroundColor Cyan
Invoke-SshWithRetry -Command $remote -Purpose "deploy"

Remove-Item $composeTemp -ErrorAction SilentlyContinue

Write-Host "Done. Container 'linuxkey-receiver' should be running on $TargetHost (port $Port)." -ForegroundColor Green
