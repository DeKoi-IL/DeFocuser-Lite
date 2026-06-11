/*
 * App.xaml.cs
 * Entry point. Multiple instances are allowed on purpose — one hub per focuser
 * (differentiated by COM port). The OS serial lock prevents two instances from
 * grabbing the same port, and each connected instance serves a per-port named
 * pipe ("DeFocuserLitePipe_<COMx>"), so there is no cross-instance collision.
 *
 * When the ASCOM driver auto-launches a hub for a specific focuser it passes
 * "--port COMx"; that port is surfaced via LaunchPort and auto-connected.
 */

using ASCOM.DeKoi.DeFocuserApp.Properties;

using System;
using System.Windows;
using System.Windows.Media;

namespace ASCOM.DeKoi.DeFocuserApp
{
    public partial class App : Application
    {
        /// <summary>
        /// COM port passed via "--port COMx" (driver auto-launch). Null if not supplied.
        /// </summary>
        public string LaunchPort { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            LaunchPort = ParseLaunchPort(e.Args);

            ApplyAccent(Settings.Default.AccentColor);

            base.OnStartup(e);
        }

        // Accepts "--port COM3" (two tokens) or "--port=COM3".
        private static string ParseLaunchPort(string[] args)
        {
            if (args == null) return null;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a.StartsWith("--port=", StringComparison.OrdinalIgnoreCase))
                {
                    string v = a.Substring("--port=".Length).Trim();
                    return v.Length > 0 ? v : null;
                }
                if (string.Equals(a, "--port", StringComparison.OrdinalIgnoreCase)
                    && i + 1 < args.Length)
                {
                    string v = args[i + 1].Trim();
                    return v.Length > 0 ? v : null;
                }
            }
            return null;
        }

        public void ApplyAccent(string hex)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hex)) hex = "#E5484D";
                var color = (Color)ColorConverter.ConvertFromString(hex);
                Resources["AccentColor"] = color;
                if (Resources["AccentBrush"] is SolidColorBrush brush)
                {
                    brush.Color = color;
                }
                else
                {
                    Resources["AccentBrush"] = new SolidColorBrush(color);
                }
            }
            catch
            {
                // Invalid hex — leave previous accent.
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { Settings.Default.Save(); } catch { }
            base.OnExit(e);
        }
    }
}
