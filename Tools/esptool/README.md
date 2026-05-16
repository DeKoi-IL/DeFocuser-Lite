# esptool.exe (required for firmware updates)

The hub bundles `esptool.exe` so it can flash ESP32-C3 firmware updates over
the existing USB-serial connection — no Python install needed on user
machines.

## Drop the binary here

Place the Windows single-file build of esptool at:

```
Tools/esptool/esptool.exe
```

Download the latest standalone Windows release from:
<https://github.com/espressif/esptool/releases>

Pick the asset named like `esptool-vX.Y.Z-win64.zip`, extract, and copy
`esptool.exe` into this folder. Tested with v4.7+ (any version that supports
the `esp32c3` chip target works).

## What uses it

- **Build** — `build.ps1` does not require esptool itself, but the installer
  (`Code/FocuserApp/Installer/Setup.iss`) picks the binary up from this folder
  via `Source: "Tools\esptool\esptool.exe"`. The line is marked
  `skipifsourcedoesntexist`, so a missing binary will silently skip bundling
  rather than break the build, but the hub's firmware-update feature will be
  disabled on user installs.
- **Runtime** — `Services/FirmwareFlasher.cs` resolves the bundled binary at
  `{app}\tools\esptool.exe` and invokes it with:
  ```
  esptool.exe --chip esp32c3 --port COMx --baud 921600 \
      --before default_reset --after hard_reset \
      write_flash 0x0 firmware.bin
  ```

## Why not auto-download?

Keeping the binary out of git avoids a ~10MB blob in version history. CI or
release pipelines can fetch it via the URL above during the release build.
