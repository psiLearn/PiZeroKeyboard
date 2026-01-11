#!/bin/bash
set -euo pipefail

if [[ $EUID -ne 0 ]]; then
    echo "Please run as root."
    exit 1
fi

CONFIGFS=/sys/kernel/config
GADGET=$CONFIGFS/usb_gadget/pi_keyboard

if [[ ! -d $CONFIGFS ]]; then
    echo "ConfigFS is not available at $CONFIGFS."
    exit 1
fi
if ! grep -q " $CONFIGFS " /proc/mounts; then
    mount -t configfs none "$CONFIGFS"
fi

if [[ -d $GADGET ]]; then
    if [[ -e /dev/hidg0 ]]; then
        echo "USB HID gadget already configured at $GADGET."
        exit 0
    fi
    echo "USB HID gadget exists but /dev/hidg0 is missing. Recreating."
    if [[ -f $GADGET/UDC ]]; then
        current_udc=$(cat "$GADGET/UDC" || true)
        if [[ -n $current_udc ]]; then
            echo "Unbinding existing gadget from $current_udc."
            echo "" > "$GADGET/UDC" || true
        fi
    fi
    rm -f "$GADGET/configs/c.1/hid.usb0" 2>/dev/null || true
    rm -rf "$GADGET/configs/c.1" "$GADGET/functions/hid.usb0" "$GADGET/strings/0x409" 2>/dev/null || true
    rmdir "$GADGET" 2>/dev/null || true
fi

mkdir -p "$GADGET"
cd "$GADGET"

echo 0x1d6b > idVendor
echo 0x0104 > idProduct
echo 0x0100 > bcdDevice
echo 0x0200 > bcdUSB

mkdir -p strings/0x409
echo "000000001" > strings/0x409/serialnumber
echo "PiZero" > strings/0x409/manufacturer
echo "PiKeyboard" > strings/0x409/product

mkdir -p configs/c.1/strings/0x409
echo "Keyboard" > configs/c.1/strings/0x409/configuration
echo 120 > configs/c.1/MaxPower

mkdir -p functions/hid.usb0
echo 1 > functions/hid.usb0/protocol
echo 1 > functions/hid.usb0/subclass
echo 8 > functions/hid.usb0/report_length
cat <<'EOF' > functions/hid.usb0/report_desc
\x05\x01\x09\x06\xa1\x01\x05\x07\x19\xe0\x29\xe7\x15\x00\x25\x01\x75\x01\x95\x08\x81\x02\x95\x01\x75\x08\x81\x01\x95\x05\x75\x01\x05\x08\x19\x01\x29\x05\x91\x02\x95\x01\x75\x03\x91\x01\x95\x06\x75\x08\x15\x00\x26\xff\x00\x05\x07\x19\x00\x29\x65\x81\x00\xc0
EOF

ln -sf functions/hid.usb0 configs/c.1/

UDC_PATH=/sys/class/udc
if [[ ! -d $UDC_PATH ]]; then
    echo "No UDC directory found at $UDC_PATH."
    exit 1
fi

available_udc=$(ls "$UDC_PATH" | head -n 1)
if [[ -z $available_udc ]]; then
    echo "No UDC device available."
    exit 1
fi

if [[ -f UDC ]]; then
    echo "" > UDC
fi
echo "$available_udc" > UDC
echo "USB HID gadget bound to $available_udc."
