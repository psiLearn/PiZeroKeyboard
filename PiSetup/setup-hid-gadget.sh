#!/bin/bash
set -euo pipefail

if [[ $EUID -ne 0 ]]; then
    echo "Please run as root."
    exit 1
fi

CONFIGFS=/sys/kernel/config
GADGET_ROOT=$CONFIGFS/usb_gadget
GADGET=$GADGET_ROOT/pi_keyboard
FORCE_RECREATE=false
VID=${HID_VENDOR:-0x1d6b}
PID=${HID_PRODUCT:-0x0104}
SERIAL=${HID_SERIAL:-000000001}
MANUFACTURER=${HID_MANUFACTURER:-PiZero}
PRODUCT=${HID_PRODUCT_STR:-PiKeyboard}

if [[ "${1:-}" == "--force" ]]; then
    FORCE_RECREATE=true
fi

if [[ ! -d $CONFIGFS ]]; then
    echo "ConfigFS is not available at $CONFIGFS."
    exit 1
fi
if ! grep -q " $CONFIGFS " /proc/mounts; then
    mount -t configfs none "$CONFIGFS"
fi
modprobe libcomposite || true
modprobe usb_f_hid || true
if [[ ! -d $GADGET_ROOT ]]; then
    echo "USB gadget configfs root missing at $GADGET_ROOT." >&2
    echo "Ensure libcomposite is available and configfs is mounted." >&2
    exit 1
fi

ensure_udc() {
    local udc_path=/sys/class/udc
    if [[ ! -d $udc_path ]]; then
        echo "No UDC directory found at $udc_path." >&2
        return 1
    fi

    local available_udc
    available_udc=$(ls "$udc_path" 2>/dev/null | head -n 1 || true)
    if [[ -z $available_udc ]]; then
        local device_id=""
        if [[ -d /sys/bus/platform/devices/3f980000.usb ]]; then
            device_id="3f980000.usb"
        fi
        if [[ -n $device_id ]]; then
            local driver_link=/sys/bus/platform/devices/$device_id/driver
            if [[ -L $driver_link ]]; then
                local driver
                driver=$(basename "$(readlink -f "$driver_link")")
                if [[ $driver == "dwc_otg" ]]; then
                    echo "USB controller is bound to dwc_otg (host mode)." >&2
                    echo "Attempting to switch to dwc2..." >&2
                    echo "$device_id" > /sys/bus/platform/drivers/dwc_otg/unbind 2>/dev/null || true
                    echo "dwc2" > /sys/bus/platform/devices/$device_id/driver_override 2>/dev/null || true
                    echo "$device_id" > /sys/bus/platform/drivers/dwc2/bind 2>/dev/null || true
                fi
            fi
        fi
    fi

    available_udc=$(ls "$udc_path" 2>/dev/null | head -n 1 || true)
    if [[ -z $available_udc ]]; then
        echo "No UDC device available." >&2
        echo "Ensure dwc2 is enabled at boot (dtoverlay=dwc2,dr_mode=peripheral)." >&2
        echo "If dwc_otg is built-in, add initcall_blacklist=dwc_otg_driver_init and reboot." >&2
        return 1
    fi

    echo "$available_udc"
}

if [[ -d $GADGET ]]; then
    needs_recreate=false
    if [[ -f $GADGET/functions/hid.usb0/report_desc ]]; then
        if LC_ALL=C grep -a -q '\\x' "$GADGET/functions/hid.usb0/report_desc"; then
            needs_recreate=true
        fi
    else
        needs_recreate=true
    fi
    if [[ $FORCE_RECREATE == false && $needs_recreate == false && -e /dev/hidg0 ]]; then
        echo "USB HID gadget already configured at $GADGET."
        exit 0
    fi
    echo "USB HID gadget exists but needs reconfiguration. Recreating."
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

echo "$VID" > idVendor
echo "$PID" > idProduct
echo 0x0100 > bcdDevice
echo 0x0200 > bcdUSB
echo 0 > bDeviceClass
echo 0 > bDeviceSubClass
echo 0 > bDeviceProtocol

mkdir -p strings/0x409
echo "$SERIAL" > strings/0x409/serialnumber
echo "$MANUFACTURER" > strings/0x409/manufacturer
echo "$PRODUCT" > strings/0x409/product

mkdir -p configs/c.1/strings/0x409
echo "Keyboard" > configs/c.1/strings/0x409/configuration
echo 0x80 > configs/c.1/bmAttributes
echo 120 > configs/c.1/MaxPower

mkdir -p functions/hid.usb0
echo 1 > functions/hid.usb0/protocol
echo 1 > functions/hid.usb0/subclass
echo 8 > functions/hid.usb0/report_length
printf '%b' '\x05\x01\x09\x06\xa1\x01\x05\x07\x19\xe0\x29\xe7\x15\x00\x25\x01\x75\x01\x95\x08\x81\x02\x95\x01\x75\x08\x81\x01\x95\x05\x75\x01\x05\x08\x19\x01\x29\x05\x91\x02\x95\x01\x75\x03\x91\x01\x95\x06\x75\x08\x15\x00\x25\x65\x05\x07\x19\x00\x29\x65\x81\x00\xc0' > functions/hid.usb0/report_desc
expected_len=63
actual_len=$(wc -c < functions/hid.usb0/report_desc)
if [[ $actual_len -ne $expected_len ]]; then
    echo "Warning: report_desc length $actual_len (expected $expected_len)." >&2
fi

ln -sf functions/hid.usb0 configs/c.1/

available_udc=$(ensure_udc) || exit 1

if [[ -f UDC ]]; then
    current_udc=$(cat UDC 2>/dev/null || true)
    if [[ -n $current_udc ]]; then
        echo "" > UDC || true
        sleep 1
    fi
fi
if ! echo "$available_udc" > UDC; then
    echo "Failed to bind gadget to $available_udc." >&2
    exit 1
fi
echo "USB HID gadget bound to $available_udc."
