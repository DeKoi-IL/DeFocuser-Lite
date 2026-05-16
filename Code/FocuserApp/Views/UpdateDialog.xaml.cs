/*
 * UpdateDialog.xaml.cs
 * Modal triggered from the title-bar "Update available" badge. Shows the
 * release notes for the latest version and either downloads/installs (which
 * exits the app) or persists the version in SkipVersion so we stop nagging.
 */

using ASCOM.DeKoi.DeFocuserApp.Properties;
using ASCOM.DeKoi.DeFocuserApp.Services;
using ASCOM.DeKoi.DeFocuserApp.ViewModels;

using System;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace ASCOM.DeKoi.DeFocuserApp.Views
{
    public partial class UpdateDialog : Window
    {
        private readonly MainViewModel mainVm;
        private readonly UpdateInfo info;
        private bool installing;

        public UpdateDialog(MainViewModel mainVm, UpdateInfo info)
        {
            InitializeComponent();
            this.mainVm = mainVm;
            this.info = info;

            HeadingText.Text = "DeFocuser Lite v" + info.LatestVersion + " is available";
            SubHeadingText.Text = "You're on v" + mainVm.CurrentAppVersion +
                                  " · download size " + FormatBytes(info.HubInstallerSize);
            ReleaseNotesText.Text = string.IsNullOrWhiteSpace(info.ReleaseNotes)
                ? "(No release notes)"
                : info.ReleaseNotes.Trim();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (installing) return;
            Close();
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Settings.Default.SkipVersion = info.LatestVersion?.ToString() ?? string.Empty;
                Settings.Default.Save();
            }
            catch { }
            Close();
        }

        private async void Install_Click(object sender, RoutedEventArgs e)
        {
            if (installing) return;

            if (mainVm.HasAscomClients)
            {
                var r = MessageBox.Show(this,
                    "Installing will disconnect " + mainVm.AscomClientCount + " ASCOM client(s). Continue?",
                    "Update", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (r != MessageBoxResult.Yes) return;
            }

            installing = true;
            InstallButton.IsEnabled = false;
            StatusText.Text = "Downloading...";

            try
            {
                var progress = new Progress<UpdateDownloadProgress>(p =>
                {
                    StatusText.Text = "Downloading " + p.Percent.ToString("F0") + "%";
                });

                string path = await UpdateInstaller.DownloadInstallerAsync(
                    info.HubInstallerUrl, info.LatestVersion, progress, CancellationToken.None);

                StatusText.Text = "Launching installer...";
                UpdateInstaller.LaunchAndExit(path);
            }
            catch (Exception ex)
            {
                installing = false;
                InstallButton.IsEnabled = true;
                StatusText.Text = "Failed: " + ex.Message;
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "—";
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
            return (bytes / 1048576.0).ToString("F1") + " MB";
        }
    }
}
