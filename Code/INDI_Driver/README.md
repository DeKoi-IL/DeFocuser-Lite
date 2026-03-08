# DeFocuser Lite - INDI Focuser Driver

An INDI focuser driver for the DeFocuser Lite ESP32-based focuser controller. This allows Linux users to control the DeFocuser Lite from INDI-compatible astronomy software such as **KStars/Ekos**, **CCDciel**, **PHD2**, and others.

## Features

- **Absolute & Relative focusing** - Move to exact positions or by step offsets
- **Position sync** - Sync the driver position to match the hardware
- **Reverse direction** - Software-configurable direction reversal
- **Abort** - Immediately halt any move in progress
- **Calibration** - Start the built-in stall-detection calibration routine
- **Set Limit** - Manually signal a physical limit during calibration
- **Auto-polling** - Position and state are polled automatically (500ms while active, 1s while idle)

## Prerequisites

- Linux (tested on Ubuntu 22.04+, Raspberry Pi OS)
- INDI Library (`libindi-dev` >= 1.9)
- CMake >= 3.10
- A C++17 compiler (GCC 7+ or Clang 5+)

### Install dependencies (Debian/Ubuntu/Raspberry Pi OS)

```bash
sudo apt-add-repository ppa:mutlaqja/ppa
sudo apt update
sudo apt install libindi-dev cmake build-essential
```

### Install dependencies (Fedora)

```bash
sudo dnf install libindi-devel cmake gcc-c++
```

## Building

```bash
cd Code/INDI_Driver
mkdir build && cd build
cmake ..
make
```

## Installing

```bash
sudo make install
```

This installs:
- `indi_defocuserlite` binary to `/usr/bin/`
- `indi_defocuserlite.xml` driver descriptor to the INDI data directory

## Usage

### With KStars/Ekos

1. Open KStars and go to **Ekos** (the observatory control panel)
2. In **Profile Editor**, click **Add** to create a new profile or edit an existing one
3. Under **Focuser**, select **DeFocuser Lite** from the dropdown
4. Click **Start** to launch the INDI server
5. The driver will appear in the INDI control panel where you can:
   - Set the serial port (default: `/dev/ttyACM0`)
   - Connect to the focuser
   - Control position, run calibration, etc.

### With indiserver (command line)

```bash
indiserver indi_defocuserlite
```

Then connect with any INDI client pointing to `localhost:7624`.

### Serial Port Permissions

The DeFocuser Lite appears as a USB CDC serial device (typically `/dev/ttyACM0`). Make sure your user has permission to access it:

```bash
sudo usermod -aG dialout $USER
```

Log out and back in for the group change to take effect.

## Serial Protocol

The driver communicates at **9600 baud** using a text-based protocol:

| Command | Response | Description |
|---------|----------|-------------|
| `COMMAND:PING` | `RESULT:PING:OK:{GUID}` | Verify device identity |
| `COMMAND:FOCUSER:GETPOSITION` | `RESULT:FOCUSER:POSITION:{n}` | Get current position |
| `COMMAND:FOCUSER:GETMAXPOSITION` | `RESULT:FOCUSER:MAXPOSITION:{n}` | Get maximum position |
| `COMMAND:FOCUSER:MOVE:{n}` | `RESULT:FOCUSER:MOVE:OK` | Move to absolute position |
| `COMMAND:FOCUSER:HALT` | `RESULT:FOCUSER:HALT:OK` | Stop movement |
| `COMMAND:FOCUSER:ISMOVING` | `RESULT:FOCUSER:ISMOVING:TRUE/FALSE` | Check if moving |
| `COMMAND:FOCUSER:SETPOSITION:{n}` | `RESULT:FOCUSER:SETPOSITION:OK` | Sync position |
| `COMMAND:FOCUSER:SETREVERSE:TRUE/FALSE` | `RESULT:FOCUSER:SETREVERSE:OK` | Set direction |
| `COMMAND:FOCUSER:CALIBRATE` | `RESULT:FOCUSER:CALIBRATE:OK` | Start calibration |
| `COMMAND:FOCUSER:SETLIMIT` | `RESULT:FOCUSER:SETLIMIT:OK` | Set limit during calibration |

## Troubleshooting

### Driver not found in KStars
Make sure you ran `sudo make install` and restart KStars.

### Permission denied on serial port
Run `sudo usermod -aG dialout $USER` and log out/in.

### Connection timeout
- Verify the device is plugged in: `ls /dev/ttyACM*`
- Check the correct port is selected in the INDI control panel
- Try unplugging and reconnecting the USB cable

### Enable debug logging
In the INDI control panel, go to the **Options** tab and enable **Debug** logging to see the raw serial commands and responses.
