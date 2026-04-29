/*
 * App.xaml.cs
 * Entry point. Enforces single instance via named mutex (matching old WinForms app)
 * so user cannot accidentally run both versions concurrently against the same hardware.
 */

using ASCOM.DeKoi.DeFocuserApp.Properties;

using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace ASCOM.DeKoi.DeFocuserApp
{
    public partial class App : Application
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        private Mutex singleInstanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            bool createdNew;
            singleInstanceMutex = new Mutex(true, "DeFocuserLiteMediator_SingleInstance", out createdNew);

            if (!createdNew)
            {
                var existing = Process.GetProcessesByName("ASCOM.DeKoi.DeFocuserApp")
                    .Concat(Process.GetProcessesByName("ASCOM.DeKoi.DeFocuserMediator"))
                    .FirstOrDefault();
                if (existing != null && existing.MainWindowHandle != IntPtr.Zero)
                {
                    ShowWindow(existing.MainWindowHandle, SW_RESTORE);
                    SetForegroundWindow(existing.MainWindowHandle);
                }
                Shutdown();
                return;
            }

            ApplyAccent(Settings.Default.AccentColor);

            base.OnStartup(e);
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
            try { singleInstanceMutex?.ReleaseMutex(); } catch { }
            try { singleInstanceMutex?.Dispose(); } catch { }
            base.OnExit(e);
        }
    }
}
