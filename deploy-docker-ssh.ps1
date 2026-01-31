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
    # Skip building images locally (expects linuxkey-*.tar already present)
    [switch]$SkipBuild,
    # Skip uploading artifacts (expects /tmp/linuxkey-*.tar, /tmp/docker-compose.yml, /tmp/setup-hid-gadget.sh on target)
    [switch]$SkipUpload,
    [switch]$SkipApt,
    [switch]$NoCache
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$skipBuildEffective = $SkipBuild -or $SkipUpload
if ($SkipUpload -and -not $SkipBuild) {
    Write-Warning "SkipUpload implies SkipBuild; build step will be skipped."
}

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

function Get-TemplateContent {
    param([string]$Path)
    if (-not (Test-Path $Path)) {
        throw "Template not found at '$Path'."
    }
    return Get-Content -Raw -Path $Path
}

function Replace-TemplateTokens {
    param(
        [string]$Template,
        [hashtable]$Tokens
    )
    $result = $Template
    foreach ($key in $Tokens.Keys) {
        $result = $result.Replace($key, [string]$Tokens[$key])
    }
    return $result
}

function Get-HttpsEnvBlock {
    param(
        [bool]$Enabled,
        [string]$CertPath,
        [string]$CertPassword,
        [int]$HttpsPort
    )
    if (-not $Enabled) { return "" }
    $lines = @("      - SENDER_HTTPS_CERT_PATH=$CertPath")
    if (-not [string]::IsNullOrWhiteSpace($CertPassword)) {
        $lines += "      - SENDER_HTTPS_CERT_PASSWORD=$CertPassword"
    }
    $lines += "      - SENDER_HTTPS_PORT=$HttpsPort"
    return ($lines -join "`n") + "`n"
}

function Get-HttpsVolumeBlock {
    param(
        [bool]$Enabled,
        [string]$CertDir
    )
    if (-not $Enabled) { return "" }
    return ("      - {0}:{0}:ro`n" -f $CertDir)
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
if (-not $skipBuildEffective) {
    $dockerOs = Ensure-DockerReady
    if ($dockerOs -ne "linux") {
        throw "Docker engine is running in '$dockerOs' mode. Switch to Linux containers and retry."
    }
    foreach ($p in @($tarPath, $senderTar)) {
        if (Test-Path $p) { Remove-Item $p -Force }
    }
}

if (-not $skipBuildEffective) {
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

if (-not $skipBuildEffective) {
    foreach ($p in @($tarPath, $senderTar)) {
        if (-not (Test-Path $p)) { throw "Build did not produce $p" }
    }
}

$composeTemp = Join-Path ([System.IO.Path]::GetTempPath()) ("linuxkey-compose-" + [System.Guid]::NewGuid().ToString("N") + ".yml")
$templatesDir = Join-Path $PSScriptRoot "deploy"
$composeTemplatePath = Join-Path $templatesDir "docker-compose.template.yml"
$remoteTemplatePath = Join-Path $templatesDir "remote-deploy.sh"
$composeTemplate = Get-TemplateContent -Path $composeTemplatePath
$httpsEnvBlock = Get-HttpsEnvBlock -Enabled $httpsEnabled -CertPath $remoteCertPath -CertPassword $HttpsCertPassword -HttpsPort $HttpsPort
$httpsVolumeBlock = Get-HttpsVolumeBlock -Enabled $httpsEnabled -CertDir $remoteCertDir
$composeContent = Replace-TemplateTokens -Template $composeTemplate -Tokens @{
    "__RECEIVER_IMAGE__" = $ReceiverImage
    "__SENDER_IMAGE__" = $SenderImage
    "__PORT__" = $Port
    "__HTTPS_ENV_BLOCK__" = $httpsEnvBlock
    "__HTTPS_VOLUME_BLOCK__" = $httpsVolumeBlock
}
Set-Content -Path $composeTemp -Value $composeContent -Encoding ASCII

Write-Host "Copying artifacts to $TargetHost..." -ForegroundColor Cyan
if ($SkipUpload) {
    Write-Warning "Skipping uploads; expecting /tmp/linuxkey-*.tar, /tmp/docker-compose.yml, and /tmp/setup-hid-gadget.sh to exist on the target."
    if ($httpsEnabled) {
        Write-Warning "HTTPS enabled; ensure $remoteCertPath exists on the target."
    }
} else {
    if ($skipBuildEffective) {
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

$remoteTemplate = Get-TemplateContent -Path $remoteTemplatePath
$remote = Replace-TemplateTokens -Template $remoteTemplate -Tokens @{
    "__CERT_TEMP__" = $remoteCertTemp
    "__CERT_DIR__" = $remoteCertDir
    "__CERT_DEST__" = $remoteCertPath
    "__SKIP_APT__" = $(if ($SkipApt) { "true" } else { "false" })
}

Write-Host "Deploying via SSH..." -ForegroundColor Cyan
Invoke-SshWithRetry -Command $remote -Purpose "deploy"

Remove-Item $composeTemp -ErrorAction SilentlyContinue

Write-Host "Done. Container 'linuxkey-receiver' should be running on $TargetHost (port $Port)." -ForegroundColor Green
