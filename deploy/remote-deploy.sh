set -e
skip_apt="__SKIP_APT__"
tar_dir="__TAR_DIR__"
receiver_tar="__RECEIVER_TAR__"
sender_tar="__SENDER_TAR__"
if [ ! -d "$tar_dir" ]; then
  sudo mkdir -p "$tar_dir"
fi
if [ ! -f "$receiver_tar" ] && [ -f /tmp/linuxkey-receiver.tar ]; then
  receiver_tar="/tmp/linuxkey-receiver.tar"
fi
if [ ! -f "$sender_tar" ] && [ -f /tmp/linuxkey-sender.tar ]; then
  sender_tar="/tmp/linuxkey-sender.tar"
fi
arch=$(uname -m)
if [ "$arch" = "armv6l" ]; then
    echo "armv6l detected: .NET container images do not support Pi Zero (armv6). Use a Pi with armv7+ or deploy without Docker." >&2
    exit 1
fi
if [ "$skip_apt" != "true" ]; then
  sudo apt-get update
fi
if ! command -v docker >/dev/null 2>&1; then
    if [ "$skip_apt" = "true" ]; then
      echo "Docker is missing and -SkipApt was set. Install Docker first or rerun without -SkipApt." >&2
      exit 1
    fi
    sudo apt-get install -y docker.io
fi
compose_cmd=""
if docker compose version >/dev/null 2>&1; then
  compose_cmd="docker compose"
elif command -v docker-compose >/dev/null 2>&1; then
  compose_cmd="docker-compose"
else
  if [ "$skip_apt" = "true" ]; then
    echo "Docker Compose is missing and -SkipApt was set. Install docker-compose or rerun without -SkipApt." >&2
    exit 1
  fi
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
echo "Configuring HID gadget..."
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
echo "Loading receiver image: $receiver_tar"
if command -v pv >/dev/null 2>&1; then
  pv "$receiver_tar" | sudo docker load
else
  sudo docker load -i "$receiver_tar"
fi
echo "Loading sender image: $sender_tar"
if command -v pv >/dev/null 2>&1; then
  pv "$sender_tar" | sudo docker load
else
  sudo docker load -i "$sender_tar"
fi
compose_project="linuxkey"
echo "Stopping existing stack (if any)..."
sudo $compose_cmd -p "$compose_project" -f /tmp/docker-compose.yml down --remove-orphans || true
echo "Starting stack..."
sudo $compose_cmd -p "$compose_project" -f /tmp/docker-compose.yml up -d --force-recreate
echo "Deploy complete."
