param(
    # Drive letter of the Raspberry Pi boot partition (e.g., "E:")
    [string]$BootDrive = "E:",
    # Enable SSH by creating the /boot/ssh marker file
    [bool]$EnableSsh = $true,
    # Optional Wi-Fi configuration written to /boot/wpa_supplicant.conf
    [string]$WifiSsid,
    [string]$WifiPsk,
    [string]$WifiCountry = "US"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$bootRoot = "$BootDrive\"
if (-not (Test-Path $bootRoot)) {
    throw "Boot drive '$BootDrive' not found. Insert the SD card and set -BootDrive accordingly."
}

$configPath = Join-Path $bootRoot "config.txt"
$cmdlinePath = Join-Path $bootRoot "cmdline.txt"

if (-not (Test-Path $configPath)) {
    throw "Missing $configPath. Ensure -BootDrive points to the Pi boot partition."
}
if (-not (Test-Path $cmdlinePath)) {
    throw "Missing $cmdlinePath. Ensure -BootDrive points to the Pi boot partition."
}

Write-Host "Enabling USB gadget (dwc2)..." -ForegroundColor Cyan
$config = Get-Content $configPath -Raw
if ($config -match '(?m)^\s*dtoverlay=dwc2') {
    if ($config -notmatch '(?m)^\s*dtoverlay=dwc2.*dr_mode=peripheral') {
        $config = [regex]::Replace($config, '(?m)^\s*dtoverlay=dwc2.*$', 'dtoverlay=dwc2,dr_mode=peripheral')
        Set-Content -Path $configPath -Value $config -Encoding ASCII
    }
} else {
    Add-Content -Path $configPath -Value "dtoverlay=dwc2,dr_mode=peripheral" -Encoding ASCII
}

$cmdline = (Get-Content $cmdlinePath -Raw).Trim()
if ($cmdline -notmatch 'modules-load=dwc2') {
    if ($cmdline -match ' rootwait') {
        $cmdline = $cmdline -replace ' rootwait', ' modules-load=dwc2 rootwait'
    } else {
        $cmdline = "$cmdline modules-load=dwc2"
    }
    Set-Content -Path $cmdlinePath -Value $cmdline -NoNewline -Encoding ASCII
}

if ($EnableSsh) {
    Write-Host "Enabling SSH..." -ForegroundColor Cyan
    $sshMarker = Join-Path $bootRoot "ssh"
    New-Item -Path $sshMarker -ItemType File -Force | Out-Null
}

if ($WifiSsid -and $WifiPsk) {
    Write-Host "Writing Wi-Fi config..." -ForegroundColor Cyan
    $wpaConf = @"
country=$WifiCountry
ctrl_interface=DIR=/var/run/wpa_supplicant GROUP=netdev
update_config=1

network={
    ssid="$WifiSsid"
    psk="$WifiPsk"
    key_mgmt=WPA-PSK
}
"@
    $wpaPath = Join-Path $bootRoot "wpa_supplicant.conf"
    Set-Content -Path $wpaPath -Value $wpaConf -Encoding ASCII
} else {
    Write-Host "Wi-Fi credentials not provided; skipping wpa_supplicant.conf." -ForegroundColor Yellow
}

Write-Host "Done. Safely eject the SD card and boot the Pi." -ForegroundColor Green
