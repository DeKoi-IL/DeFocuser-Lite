/*
 * FirmwareBoards.cs
 * The board revisions we publish firmware for. Ids mirror BOARD_ID in
 * Arduino_Firmware.ino and the $FirmwareVariants table in build.ps1 — all three
 * lists have to agree, because the id is what ties a release asset to the
 * pinout actually running on the device.
 */

using System;
using System.Collections.Generic;
using System.Linq;

namespace ASCOM.DeKoi.DeFocuserApp.Services
{
    public sealed class FirmwareBoard
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string EsptoolChip { get; }

        public FirmwareBoard(string id, string displayName, string esptoolChip)
        {
            Id = id;
            DisplayName = displayName;
            EsptoolChip = esptoolChip;
        }

        // ComboBox binds to this directly.
        public override string ToString() => DisplayName;
    }

    public static class FirmwareBoards
    {
        // Old board first: it is what every unit built before the current
        // revision runs, so it is also the safest default for an unknown device.
        public static readonly FirmwareBoard Esp32C3Old =
            new FirmwareBoard("esp32c3-old", "XIAO ESP32-C3 (old)", "esp32c3");

        public static readonly FirmwareBoard Esp32C3 =
            new FirmwareBoard("esp32c3", "XIAO ESP32-C3", "esp32c3");

        public static readonly FirmwareBoard Esp32S3 =
            new FirmwareBoard("esp32s3", "XIAO ESP32-S3", "esp32s3");

        public static IReadOnlyList<FirmwareBoard> All { get; } =
            new[] { Esp32C3Old, Esp32C3, Esp32S3 };

        public static FirmwareBoard Default => Esp32C3Old;

        public static FirmwareBoard Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            return All.FirstOrDefault(b => string.Equals(b.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public static FirmwareBoard FindOrDefault(string id) => Find(id) ?? Default;
    }
}
