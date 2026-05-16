/*
 * MainWindow.xaml.cs
 * Hosts the chrome-less WPF window. Owns title-bar drag, window controls,
 * console auto-scroll, progress-bar fill width, and settings persistence.
 */

using ASCOM.DeKoi.DeFocuserApp.Properties;
using ASCOM.DeKoi.DeFocuserApp.Services;
using ASCOM.DeKoi.DeFocuserApp.ViewModels;
using ASCOM.DeKoi.DeFocuserApp.Views;

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using DragEventArgs = System.Windows.DragEventArgs;
using MessageBox = System.Windows.MessageBox;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace ASCOM.DeKoi.DeFocuserApp
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel vm;
        private NotifyIcon trayIcon;
        private bool forceClose;

        public MainWindow()
        {
            InitializeComponent();

            vm = new MainViewModel();
            DataContext = vm;
            vm.LogAppended += Vm_LogAppended;
            vm.LogCleared += Vm_LogCleared;
            vm.PropertyChanged += Vm_PropertyChanged;

            RestoreWindowPlacement();
            BuildTrayIcon();

            Loaded += OnLoaded;
            Closing += OnClosing;
            Closed += OnClosedWindow;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateProgressFill();
            vm.TryAutoConnectOnStartup();
            _ = CheckForUpdatesOnStartupAsync();
        }

        private async Task CheckForUpdatesOnStartupAsync()
        {
            try
            {
                var info = await UpdateChecker.CheckAsync();
                if (info == null) return;

                Settings.Default.LastUpdateCheckTime = DateTime.UtcNow;
                Settings.Default.Save();

                // Respect "skip this version" choice from a previous dialog.
                string skipped = Settings.Default.SkipVersion ?? string.Empty;
                if (info.LatestVersion != null
                    && string.Equals(skipped, info.LatestVersion.ToString(), StringComparison.Ordinal))
                {
                    // Still publish to the VM so Settings popup can show it,
                    // but the badge is suppressed because we re-check the
                    // skip rule there.
                }

                Dispatcher.Invoke(() => vm.SetUpdateInfo(info));
            }
            catch
            {
                // No network / GitHub down — never user-facing on startup.
            }
        }

        private void Vm_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.PercentOfTravel))
            {
                UpdateProgressFill();
            }
        }

        private void Vm_LogAppended(object sender, EventArgs e)
        {
            if (vm.LogAutoScroll)
            {
                ConsoleScroller.ScrollToEnd();
            }
        }

        private void Vm_LogCleared(object sender, EventArgs e)
        {
            ConsoleScroller.ScrollToHome();
        }

        private void ProgressTrack_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateProgressFill();
        }

        private void UpdateProgressFill()
        {
            if (ProgressTrack == null || ProgressFill == null) return;
            double w = ProgressTrack.ActualWidth - ProgressTrack.BorderThickness.Left - ProgressTrack.BorderThickness.Right;
            if (w < 0) w = 0;
            double pct = vm.PercentOfTravel;
            ProgressFill.Width = Math.Max(0, Math.Min(w, w * pct / 100.0));
        }

        // ---------------- Title bar ----------------

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount >= 2)
            {
                ToggleMaximize();
                return;
            }
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { }
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            if (vm.UpdateInfo == null) return;
            var dlg = new UpdateDialog(vm, vm.UpdateInfo) { Owner = this };
            dlg.ShowDialog();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsVm = new SettingsViewModel(vm);
            var dlg = new SettingsWindow(settingsVm) { Owner = this };
            dlg.ShowDialog();
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void HaltButton_Click(object sender, RoutedEventArgs e)
        {
            // Click handler exists so HALT button surfaces even when CommandManager
            // hasn't yet flipped CanExecute (e.g. mid-async). The command itself
            // performs the actual work and is bound via Command="{Binding HaltCommand}".
        }

        // ---------------- Tray ----------------

        private void BuildTrayIcon()
        {
            try
            {
                trayIcon = new NotifyIcon
                {
                    Icon = new System.Drawing.Icon(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dekoi.ico")),
                    Text = "DeFocuser Controller",
                    Visible = false,
                    ContextMenuStrip = new ContextMenuStrip()
                };
                trayIcon.ContextMenuStrip.Items.Add("Show", null, (s, e) => RestoreFromTray());
                trayIcon.ContextMenuStrip.Items.Add("Force Close", null, (s, e) => { forceClose = true; Close(); });
                trayIcon.DoubleClick += (s, e) => RestoreFromTray();
            }
            catch
            {
                trayIcon = null;
            }
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            if (trayIcon != null) trayIcon.Visible = false;
        }

        // ---------------- Closing / persistence ----------------

        private void OnClosing(object sender, CancelEventArgs e)
        {
            if (!forceClose && vm.HasAscomClients)
            {
                e.Cancel = true;
                Hide();
                if (trayIcon != null)
                {
                    trayIcon.Visible = true;
                    trayIcon.ShowBalloonTip(3000, "DeFocuser Controller",
                        "Still serving " + vm.AscomClientCount + " ASCOM client(s). Right-click tray icon to force close.",
                        ToolTipIcon.Info);
                }
                return;
            }

            SaveWindowPlacement();
            try { Settings.Default.Save(); } catch { }

            try { vm.Dispose(); } catch { }
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon = null;
            }
        }

        private void OnClosedWindow(object sender, EventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void RestoreWindowPlacement()
        {
            try
            {
                double w = Settings.Default.WindowWidth;
                double h = Settings.Default.WindowHeight;
                double l = Settings.Default.WindowLeft;
                double t = Settings.Default.WindowTop;

                if (!double.IsNaN(w) && w > 200) Width = w;
                if (!double.IsNaN(h) && h > 200) Height = h;
                if (!double.IsNaN(l) && !double.IsNaN(t))
                {
                    WindowStartupLocation = WindowStartupLocation.Manual;
                    Left = l;
                    Top = t;
                }
            }
            catch { }
        }

        private void SaveWindowPlacement()
        {
            try
            {
                if (WindowState == WindowState.Normal)
                {
                    Settings.Default.WindowWidth = Width;
                    Settings.Default.WindowHeight = Height;
                    Settings.Default.WindowLeft = Left;
                    Settings.Default.WindowTop = Top;
                }
            }
            catch { }
        }
    }
}
