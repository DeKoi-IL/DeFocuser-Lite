/*
 * SettingsViewModel.cs
 * Backs the modal Settings window. Currently exposes only the Update section
 * (manual check + install for hub, flash for firmware). New sections will be
 * added here as the popup grows.
 */

using ASCOM.DeKoi.DeFocuserApp.Properties;
using ASCOM.DeKoi.DeFocuserApp.Services;

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ASCOM.DeKoi.DeFocuserApp.ViewModels
{
    public class SettingsViewModel : ObservableObject
    {
        private readonly MainViewModel mainVm;

        public SettingsViewModel(MainViewModel mainVm)
        {
            this.mainVm = mainVm;
            CheckForUpdatesCommand = new RelayCommand(_ => _ = CheckAsync(), _ => !isChecking);
            InstallUpdateCommand = new RelayCommand(_ => _ = InstallAsync(),
                _ => !isChecking && mainVm.UpdateInfo != null && mainVm.UpdateInfo.HubAvailable);
            FlashFirmwareCommand = new RelayCommand(_ => _ = FlashAsync(),
                _ => !isChecking && !mainVm.IsFlashingFirmware
                     && mainVm.FirmwareUpdateAvailable && mainVm.IsConnected);

            lastChecked = Settings.Default.LastUpdateCheckTime;
            status = mainVm.UpdateAvailable
                ? "v" + mainVm.UpdateInfo.LatestVersion + " available"
                : "Click to check";

            mainVm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.UpdateInfo)
                    || e.PropertyName == nameof(MainViewModel.FirmwareUpdateAvailable)
                    || e.PropertyName == nameof(MainViewModel.IsFlashingFirmware)
                    || e.PropertyName == nameof(MainViewModel.IsConnected))
                {
                    OnPropertyChanged(nameof(HubUpdateAvailable));
                    OnPropertyChanged(nameof(FirmwareUpdateAvailable));
                    OnPropertyChanged(nameof(HubVersionDisplay));
                    OnPropertyChanged(nameof(LatestHubVersionDisplay));
                    OnPropertyChanged(nameof(LatestFirmwareVersionDisplay));
                    OnPropertyChanged(nameof(FirmwareVersionDisplay));
                    CommandManager.InvalidateRequerySuggested();
                }
            };
        }

        public RelayCommand CheckForUpdatesCommand { get; }
        public RelayCommand InstallUpdateCommand { get; }
        public RelayCommand FlashFirmwareCommand { get; }

        // Settings popup ignores SkipVersion — user came here to act explicitly.
        public bool HubUpdateAvailable => mainVm.UpdateInfo != null && mainVm.UpdateInfo.HubAvailable;
        public bool FirmwareUpdateAvailable => mainVm.FirmwareUpdateAvailable;

        public string HubVersionDisplay => "v" + mainVm.CurrentAppVersion;
        public string LatestHubVersionDisplay =>
            mainVm.UpdateInfo?.LatestVersion != null ? "v" + mainVm.UpdateInfo.LatestVersion : "—";

        public string FirmwareVersionDisplay => mainVm.CurrentFirmwareVersionDisplay;
        public string LatestFirmwareVersionDisplay =>
            mainVm.UpdateInfo?.FirmwareVersion != null ? "v" + mainVm.UpdateInfo.FirmwareVersion : "—";

        private string status;
        public string Status { get => status; private set => SetField(ref status, value); }

        private DateTime lastChecked;
        public DateTime LastChecked
        {
            get => lastChecked;
            private set { if (SetField(ref lastChecked, value)) OnPropertyChanged(nameof(LastCheckedDisplay)); }
        }

        public string LastCheckedDisplay =>
            lastChecked > new DateTime(2001, 1, 1)
                ? "Last checked " + lastChecked.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                : "Never checked";

        private bool isChecking;
        public bool IsChecking
        {
            get => isChecking;
            private set
            {
                if (SetField(ref isChecking, value))
                    CommandManager.InvalidateRequerySuggested();
            }
        }

        private async Task CheckAsync()
        {
            IsChecking = true;
            Status = "Checking...";
            try
            {
                var info = await UpdateChecker.CheckAsync();
                mainVm.SetUpdateInfo(info);
                LastChecked = DateTime.UtcNow;
                Settings.Default.LastUpdateCheckTime = LastChecked;
                Settings.Default.Save();

                if (info.HubAvailable)
                    Status = "v" + info.LatestVersion + " available";
                else if (info.LatestVersion != null)
                    Status = "Up to date (v" + info.CurrentVersion?.ToString(3) + ")";
                else
                    Status = "No releases found";
            }
            catch (Exception ex)
            {
                Status = "Check failed: " + ex.Message;
            }
            finally
            {
                IsChecking = false;
            }
        }

        private async Task InstallAsync()
        {
            var info = mainVm.UpdateInfo;
            if (info == null || !info.HubAvailable) return;

            if (mainVm.HasAscomClients)
            {
                var r = MessageBox.Show(
                    "Installing will disconnect " + mainVm.AscomClientCount + " ASCOM client(s). Continue?",
                    "Update", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (r != MessageBoxResult.Yes) return;
            }

            IsChecking = true;
            Status = "Downloading...";
            try
            {
                var progress = new Progress<UpdateDownloadProgress>(p =>
                {
                    Status = "Downloading " + p.Percent.ToString("F0") + "%";
                });

                string path = await UpdateInstaller.DownloadInstallerAsync(
                    info.HubInstallerUrl, info.LatestVersion, progress, CancellationToken.None);

                Status = "Launching installer...";
                UpdateInstaller.LaunchAndExit(path);
            }
            catch (Exception ex)
            {
                Status = "Install failed: " + ex.Message;
                IsChecking = false;
            }
        }

        private async Task FlashAsync()
        {
            var info = mainVm.UpdateInfo;
            if (info == null || info.FirmwareBinUrl == null) return;

            var r = MessageBox.Show(
                "Flash firmware v" + info.FirmwareVersion + " to the connected device? The serial connection will be temporarily released.",
                "Firmware update", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;

            IsChecking = true;
            Status = "Flashing firmware...";
            try
            {
                bool ok = await mainVm.FlashFirmwareAsync(info.FirmwareBinUrl, info.FirmwareVersion, CancellationToken.None);
                Status = ok ? "Firmware updated" : "Firmware flash failed";
            }
            finally
            {
                IsChecking = false;
            }
        }
    }
}
