/*
 * SettingsViewModel.cs
 * Backs the modal Settings window. Exposes the Updates section (manual check
 * + install for hub, flash for firmware) and the Stall detection section
 * (full set of tuning knobs). Stall-detection state lives on MainViewModel;
 * this VM just proxies through so edits flow straight to the device.
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
                     && mainVm.FirmwareUpdateAvailable && mainVm.IsConnected
                     && !mainVm.BoardMismatch);
            ResetStallDefaultsCommand = new RelayCommand(_ => mainVm.ResetStallSettingsToDefaults(),
                _ => mainVm.IsConnected);

            lastChecked = Settings.Default.LastUpdateCheckTime;
            status = mainVm.UpdateAvailable
                ? "v" + mainVm.UpdateInfo.LatestVersion + " available"
                : "Click to check";

            mainVm.PropertyChanged += (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(MainViewModel.SelectedBoard):      OnPropertyChanged(nameof(SelectedBoard));      break;
                    case nameof(MainViewModel.DeviceBoardDisplay): OnPropertyChanged(nameof(DeviceBoardDisplay)); break;
                    case nameof(MainViewModel.BoardMismatch):
                        OnPropertyChanged(nameof(BoardMismatch));
                        CommandManager.InvalidateRequerySuggested();
                        break;
                }

                if (e.PropertyName == nameof(MainViewModel.UpdateInfo)
                    || e.PropertyName == nameof(MainViewModel.FirmwareUpdateAvailable)
                    || e.PropertyName == nameof(MainViewModel.IsFlashingFirmware)
                    || e.PropertyName == nameof(MainViewModel.IsConnected))
                {
                    OnPropertyChanged(nameof(DeviceBoardDisplay));
                    OnPropertyChanged(nameof(BoardMismatch));
                    OnPropertyChanged(nameof(HubUpdateAvailable));
                    OnPropertyChanged(nameof(FirmwareUpdateAvailable));
                    OnPropertyChanged(nameof(HubVersionDisplay));
                    OnPropertyChanged(nameof(LatestHubVersionDisplay));
                    OnPropertyChanged(nameof(LatestFirmwareVersionDisplay));
                    OnPropertyChanged(nameof(FirmwareVersionDisplay));
                    OnPropertyChanged(nameof(IsConnected));
                    CommandManager.InvalidateRequerySuggested();
                }

                switch (e.PropertyName)
                {
                    case nameof(MainViewModel.StallThreshold): OnPropertyChanged(nameof(StallThreshold)); break;
                    case nameof(MainViewModel.StallCount):     OnPropertyChanged(nameof(StallCount));     break;
                    case nameof(MainViewModel.StallWindow):    OnPropertyChanged(nameof(StallWindow));    break;
                    case nameof(MainViewModel.StallGrace):     OnPropertyChanged(nameof(StallGrace));     break;
                    case nameof(MainViewModel.StallEnabled):   OnPropertyChanged(nameof(StallEnabled));   break;
                }
            };
        }

        public RelayCommand CheckForUpdatesCommand { get; }
        public RelayCommand InstallUpdateCommand { get; }
        public RelayCommand FlashFirmwareCommand { get; }
        public RelayCommand ResetStallDefaultsCommand { get; }

        public bool IsConnected => mainVm.IsConnected;

        public System.Collections.Generic.IReadOnlyList<FirmwareBoard> BoardOptions => mainVm.BoardOptions;

        public FirmwareBoard SelectedBoard
        {
            get => mainVm.SelectedBoard;
            set => mainVm.SelectedBoard = value;
        }

        public string DeviceBoardDisplay => mainVm.DeviceBoardDisplay;
        public bool BoardMismatch => mainVm.BoardMismatch;

        // Lived in the driver's Setup dialog until that was removed; the ASCOM
        // Profile is still the storage, the hub is just the UI for it now.
        private bool driverTraceEnabled = AscomSlot.GetTraceEnabled();
        public bool DriverTraceEnabled
        {
            get => driverTraceEnabled;
            set
            {
                if (SetField(ref driverTraceEnabled, value))
                {
                    AscomSlot.SetTraceEnabled(value);
                }
            }
        }

        public int StallThresholdMin => mainVm.StallThresholdMin;
        public int StallThresholdMax => mainVm.StallThresholdMax;
        public int StallThreshold
        {
            get => mainVm.StallThreshold;
            set => mainVm.StallThreshold = value;
        }

        public int StallCountMin => mainVm.StallCountMin;
        public int StallCountMax => mainVm.StallCountMax;
        public int StallCount
        {
            get => mainVm.StallCount;
            set => mainVm.StallCount = value;
        }

        public int StallWindowMin => mainVm.StallWindowMin;
        public int StallWindowMax => mainVm.StallWindowMax;
        public int StallWindow
        {
            get => mainVm.StallWindow;
            set => mainVm.StallWindow = value;
        }

        public int StallGraceMin => mainVm.StallGraceMin;
        public int StallGraceMax => mainVm.StallGraceMax;
        public int StallGrace
        {
            get => mainVm.StallGrace;
            set => mainVm.StallGrace = value;
        }

        public bool StallEnabled
        {
            get => mainVm.StallEnabled;
            set => mainVm.StallEnabled = value;
        }

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

            // Separate from the check above: a client can be holding the driver
            // DLL without an open pipe, and the silent installer closes it
            // regardless.
            if (!UpdateInstaller.ConfirmAppsWillClose()) return;

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
            if (info == null) return;

            var board = mainVm.SelectedBoard ?? FirmwareBoards.Default;
            string url = info.FirmwareUrlFor(board.Id);
            if (url == null) return;

            // A release that predates per-board assets only carries one .bin,
            // and we can't tell which pinout it holds. Say so rather than
            // flashing something that might not match the hardware.
            string caveat = info.HasBoardSpecificFirmware
                ? "Board: " + board.DisplayName + "."
                : "This release predates per-board firmware, so only one binary is published "
                  + "and it may not match " + board.DisplayName + ".";

            var r = MessageBox.Show(
                "Flash firmware v" + info.FirmwareVersion + " to the connected device?\n\n"
                + caveat + "\n\nThe serial connection will be temporarily released.",
                "Firmware update", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;

            IsChecking = true;
            Status = "Flashing firmware...";
            try
            {
                bool ok = await mainVm.FlashFirmwareAsync(url, info.FirmwareVersion, CancellationToken.None);
                Status = ok ? "Firmware updated" : "Firmware flash failed";
            }
            finally
            {
                IsChecking = false;
            }
        }
    }
}
