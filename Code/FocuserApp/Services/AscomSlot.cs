/*
 * AscomSlot.cs
 * The hub's view of the ASCOM driver's Profile. Transport configuration used to
 * live in the driver's own Setup dialog; it lives here now, so the hub is what
 * writes the COM port (and trace flag) the driver reads at Connect.
 *
 * Keys must match FocuserDriver's comPortProfileName / traceStateProfileName.
 */

using ASCOM.Utilities;

using System;

namespace ASCOM.DeKoi.DeFocuserApp.Services
{
    public static class AscomSlot
    {
        private const string DriverId = "ASCOM.DeKoi.DeFocuserLite";
        private const string DeviceType = "Focuser";
        private const string ComPortValueName = "COM Port";
        private const string TraceValueName = "Trace Level";

        /// <summary>
        /// COM port the ASCOM driver is currently pointed at, or empty. Never
        /// throws — an unregistered driver or a locked Profile just reads empty.
        /// </summary>
        public static string GetClaimedPort()
        {
            try
            {
                using (var profile = new Profile { DeviceType = DeviceType })
                {
                    if (!profile.IsRegistered(DriverId)) return string.Empty;
                    return profile.GetValue(DriverId, ComPortValueName, string.Empty, string.Empty) ?? string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Points the ASCOM driver at <paramref name="portName"/>. Returns false
        /// if the Profile could not be written (driver not registered yet, or
        /// no permission), so the caller can surface it rather than silently
        /// leaving the driver aimed somewhere else.
        /// </summary>
        public static bool ClaimPort(string portName)
        {
            if (string.IsNullOrWhiteSpace(portName)) return false;

            try
            {
                using (var profile = new Profile { DeviceType = DeviceType })
                {
                    if (!profile.IsRegistered(DriverId)) return false;
                    profile.WriteValue(DriverId, ComPortValueName, portName.Trim().ToUpperInvariant());
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool GetTraceEnabled()
        {
            try
            {
                using (var profile = new Profile { DeviceType = DeviceType })
                {
                    if (!profile.IsRegistered(DriverId)) return false;
                    string raw = profile.GetValue(DriverId, TraceValueName, string.Empty, "false");
                    return bool.TryParse(raw, out bool value) && value;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool SetTraceEnabled(bool enabled)
        {
            try
            {
                using (var profile = new Profile { DeviceType = DeviceType })
                {
                    if (!profile.IsRegistered(DriverId)) return false;
                    profile.WriteValue(DriverId, TraceValueName, enabled.ToString());
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
