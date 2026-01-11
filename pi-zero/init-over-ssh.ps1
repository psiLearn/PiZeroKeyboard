param(
    # Hostname or IP of the Raspberry Pi (SSH reachable)
    [Parameter(Mandatory = $true)]
    [Alias("Host")]
    [string]$TargetHost,
    [string]$User = "pi",
    [int]$SshPort = 22,
    # Path to setup-hid-gadget.sh on the Pi (optional)
    [string]$HidSetupPath = "/boot/linuxkey/setup-hid-gadget.sh",
    # Copy the local setup-hid-gadget.sh to the Pi before running
    [bool]$CopyHidSetup = $true,
    # Whether to run the HID setup script (if present)
    [bool]$RunHidSetup = $true,
    # Reboot after enabling dwc2 (recommended)
    [bool]$Reboot = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Get-Command ssh -ErrorAction SilentlyContinue)) {
    throw "ssh command not found. Install OpenSSH client."
}
$remoteHidPath = $HidSetupPath -replace '\\', '/'
$remoteHidDir = $remoteHidPath -replace '/[^/]+$', ''
if ($CopyHidSetup -and -not (Get-Command scp -ErrorAction SilentlyContinue)) {
    throw "scp command not found. Install OpenSSH client."
}

$hidPath = if ($RunHidSetup) { $remoteHidPath } else { "" }
$rebootFlag = if ($Reboot) { "1" } else { "0" }

if ($CopyHidSetup) {
    $remoteTemp = "/tmp/linuxkey-setup-hid-gadget.sh"
    Write-Host "Copying setup-hid-gadget.sh to ${TargetHost}:$remoteTemp..." -ForegroundColor Cyan
    scp -P $SshPort (Join-Path $PSScriptRoot "..\\PiSetup\\setup-hid-gadget.sh") "${User}@${TargetHost}:$remoteTemp"
    ssh -p $SshPort "${User}@${TargetHost}" "sudo mkdir -p '$remoteHidDir' && sudo mv '$remoteTemp' '$remoteHidPath' && sudo chmod +x '$remoteHidPath'"
}

$remote = @'
set -e
CFG="/boot/config.txt"
CMD="/boot/cmdline.txt"
if [ -f /boot/firmware/config.txt ]; then
  CFG="/boot/firmware/config.txt"
  CMD="/boot/firmware/cmdline.txt"
fi
if [ ! -f "$CFG" ] || [ ! -f "$CMD" ]; then
  echo "Missing boot config at $CFG or $CMD." >&2
  exit 1
fi
if grep -q '^dtoverlay=dwc2' "$CFG"; then
  if ! grep -q '^dtoverlay=dwc2.*dr_mode=peripheral' "$CFG"; then
    sudo sed -i 's/^dtoverlay=dwc2.*/dtoverlay=dwc2,dr_mode=peripheral/' "$CFG"
  fi
else
  echo 'dtoverlay=dwc2,dr_mode=peripheral' | sudo tee -a "$CFG" >/dev/null
fi
if ! grep -q 'modules-load=dwc2' "$CMD"; then
  if grep -q ' rootwait' "$CMD"; then
    sudo sed -i 's/ rootwait/ rootwait modules-load=dwc2/' "$CMD"
  else
    sudo sh -c "printf '%s modules-load=dwc2' \"$(cat $CMD)\" > $CMD"
  fi
fi
sudo modprobe libcomposite || true
if ! mountpoint -q /sys/kernel/config; then
  sudo mount -t configfs none /sys/kernel/config
fi
HID_SETUP_PATH="__HID_SETUP__"
if [ -n "$HID_SETUP_PATH" ]; then
  if [ ! -f "$HID_SETUP_PATH" ]; then
    echo "HID setup script not found at $HID_SETUP_PATH. Skipping."
  else
    if [ -z "$(ls /sys/class/udc 2>/dev/null)" ]; then
      echo "UDC not available yet. Reboot and rerun to complete HID setup."
      if [ "__REBOOT__" = "1" ]; then
        sudo reboot
      fi
      exit 0
    fi
    sudo bash "$HID_SETUP_PATH"
  fi
fi
if [ "__REBOOT__" = "1" ]; then
  sudo reboot
else
  echo "Reboot skipped."
fi
'@

$remote = $remote.Replace("__HID_SETUP__", $hidPath).Replace("__REBOOT__", $rebootFlag)

Write-Host "Enabling dwc2 and HID setup on $TargetHost..." -ForegroundColor Cyan
ssh -p $SshPort "${User}@${TargetHost}" "$remote"
if ($LASTEXITCODE -ne 0) {
    throw "Remote init failed with exit code $LASTEXITCODE."
}
