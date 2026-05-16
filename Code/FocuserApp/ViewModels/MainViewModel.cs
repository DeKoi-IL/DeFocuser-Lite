/*
 * MainViewModel.cs
 * Drives the entire WPF window. Owns SerialManager + PipeServer, exposes state
 * and commands for the four cards (Connection, Settings, Position, Movement)
 * plus the Console.
 */

using ASCOM.DeKoi.DeFocuserApp.Properties;
using ASCOM.DeKoi.DeFocuserApp.Services;

using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ASCOM.DeKoi.DeFocuserApp.ViewModels
{
    public class MainViewModel : ObservableObject, IDisposable
    {
        private readonly SerialManager serial;
        private readonly PipeServer pipes;
        private readonly DispatcherTimer pollTimer;
        private readonly Dispatcher uiDispatcher;

        public ObservableCollection<string> AvailablePorts { get; } = new ObservableCollection<string>();
        public ObservableCollection<int> AvailableBauds { get; } = new ObservableCollection<int> { 9600, 19200, 57600, 115200 };
        public ObservableCollection<int> StepSizeOptions { get; } = new ObservableCollection<int> { 1, 10, 100, 1000 };
        public ObservableCollection<string> SpeedOptions { get; } = new ObservableCollection<string> { "Fast", "Normal", "Slow" };

        public ObservableCollection<LogEntry> LogEntries { get; } = new ObservableCollection<LogEntry>();

        private string selectedPort;
        public string SelectedPort
        {
            // No side effects on Settings here — LastComPort is only persisted
            // after a successful Connect (in ConnectAsync). Otherwise the
            // auto-select-first-port fallback at startup would forge a saved
            // port and trigger spurious auto-connects.
            get => selectedPort;
            set => SetField(ref selectedPort, value);
        }

        private int selectedBaud = 57600;
        public int SelectedBaud { get => selectedBaud; set => SetField(ref selectedBaud, value); }

        private bool autoDetect;
        public bool AutoDetect { get => autoDetect; set => SetField(ref autoDetect, value); }

        private bool autoConnectOnOpen = true;
        public bool AutoConnectOnOpen
        {
            get => autoConnectOnOpen;
            set { if (SetField(ref autoConnectOnOpen, value)) Settings.Default.AutoConnectOnStartup = value; }
        }

        private bool isConnected;
        public bool IsConnected
        {
            get => isConnected;
            private set
            {
                if (SetField(ref isConnected, value))
                {
                    OnPropertyChanged(nameof(ConnectionStatusText));
                    OnPropertyChanged(nameof(ConnectionStatusKind));
                    OnPropertyChanged(nameof(IsDisconnected));
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool IsDisconnected => !isConnected;

        public string ConnectionStatusText => isConnected ? "Connected" : "Disconnected";
        public string ConnectionStatusKind => isConnected ? "Good" : "Neutral";

        private int position;
        public int Position
        {
            get => position;
            private set
            {
                if (SetField(ref position, value))
                {
                    OnPropertyChanged(nameof(PercentOfTravel));
                    OnPropertyChanged(nameof(DeltaSteps));
                }
            }
        }

        private int maxPosition = 0;
        public int MaxPosition
        {
            get => maxPosition;
            private set
            {
                if (SetField(ref maxPosition, value))
                {
                    OnPropertyChanged(nameof(PercentOfTravel));
                }
            }
        }

        private int target;
        public int Target
        {
            get => target;
            set
            {
                if (SetField(ref target, value))
                {
                    OnPropertyChanged(nameof(DeltaSteps));
                }
            }
        }

        public double PercentOfTravel
        {
            get
            {
                if (maxPosition <= 0) return 0;
                double v = (double)position / maxPosition * 100.0;
                if (v < 0) v = 0;
                if (v > 100) v = 100;
                return v;
            }
        }

        public int DeltaSteps => target - position;

        private int stepSize = 100;
        public int StepSize
        {
            get => stepSize;
            set
            {
                if (SetField(ref stepSize, value))
                {
                    OnPropertyChanged(nameof(StepSizeX10));
                }
            }
        }

        // Pre-computed 10x value for « / » jog button labels. Avoids
        // string-appending "0" to a thousands-formatted string (which
        // produced bugs like "1,0000" when StepSize was 1000).
        public int StepSizeX10 => stepSize * 10;

        public int StallThresholdMin => SerialManager.StallThresholdMin;
        public int StallThresholdMax => SerialManager.StallThresholdMax;

        private int stallThreshold = 211;
        public int StallThreshold
        {
            get => stallThreshold;
            set
            {
                int clamped = Math.Max(StallThresholdMin, Math.Min(StallThresholdMax, value));
                if (SetField(ref stallThreshold, clamped))
                {
                    if (isConnected)
                    {
                        Task.Run(() =>
                        {
                            try { serial.SetStallThreshold(clamped); }
                            catch (Exception ex) { Log(LogKind.Err, "SetStallThreshold failed: " + ex.Message); }
                        });
                    }
                }
            }
        }

        private string speed = "Fast";
        public string Speed
        {
            get => speed;
            set
            {
                if (SetField(ref speed, value))
                {
                    if (isConnected)
                    {
                        Task.Run(() =>
                        {
                            try { serial.SetSpeed(value); }
                            catch (Exception ex)
                            {
                                Log(LogKind.Err, "SetSpeed failed: " + ex.Message);
                            }
                        });
                    }
                }
            }
        }

        private bool reverse;
        public bool Reverse
        {
            get => reverse;
            set
            {
                if (SetField(ref reverse, value))
                {
                    if (isConnected)
                    {
                        try { serial.SetReverse(value); }
                        catch (Exception ex) { Log(LogKind.Err, "SetReverse failed: " + ex.Message); }
                    }
                }
            }
        }

        private int backlashCompensation;
        public int BacklashCompensation
        {
            get => backlashCompensation;
            set
            {
                if (SetField(ref backlashCompensation, Math.Max(0, value)))
                {
                    Settings.Default.BacklashCompensation = backlashCompensation;
                }
            }
        }

        private bool isMoving;
        public bool IsMoving
        {
            get => isMoving;
            private set
            {
                if (SetField(ref isMoving, value))
                {
                    OnPropertyChanged(nameof(MovementStatusText));
                    OnPropertyChanged(nameof(MovementStatusKind));
                    // Background-thread state changes don't fire CommandManager
                    // requery automatically — force it so HALT/Move/Jog buttons
                    // re-evaluate CanExecute as soon as the firmware reports Idle.
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private bool isCalibrating;
        public bool IsCalibrating
        {
            get => isCalibrating;
            private set
            {
                if (SetField(ref isCalibrating, value))
                {
                    OnPropertyChanged(nameof(MovementStatusText));
                    OnPropertyChanged(nameof(MovementStatusKind));
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string MovementStatusText
        {
            get
            {
                if (isCalibrating) return "Calibrating";
                if (isMoving) return "Moving";
                return "Idle";
            }
        }

        public string MovementStatusKind
        {
            get
            {
                if (isCalibrating) return "Warn";
                if (isMoving) return "Warn";
                return "Good";
            }
        }

        private bool logEnabled = true;
        public bool LogEnabled { get => logEnabled; set => SetField(ref logEnabled, value); }

        private bool logAutoScroll = true;
        public bool LogAutoScroll { get => logAutoScroll; set => SetField(ref logAutoScroll, value); }

        private bool connectionExpanded = true;
        public bool ConnectionExpanded
        {
            get => connectionExpanded;
            set { if (SetField(ref connectionExpanded, value)) Settings.Default.ConnectionExpanded = value; }
        }

        private bool settingsExpanded = true;
        public bool SettingsExpanded
        {
            get => settingsExpanded;
            set { if (SetField(ref settingsExpanded, value)) Settings.Default.SettingsExpanded = value; }
        }

        private bool positionExpanded = true;
        public bool PositionExpanded
        {
            get => positionExpanded;
            set { if (SetField(ref positionExpanded, value)) Settings.Default.PositionExpanded = value; }
        }

        private bool movementExpanded = true;
        public bool MovementExpanded
        {
            get => movementExpanded;
            set { if (SetField(ref movementExpanded, value)) Settings.Default.MovementExpanded = value; }
        }

        private bool showProgress = true;
        public bool ShowProgress
        {
            get => showProgress;
            set { if (SetField(ref showProgress, value)) Settings.Default.ShowProgress = value; }
        }

        private bool showConsole = true;
        public bool ShowConsole
        {
            get => showConsole;
            set { if (SetField(ref showConsole, value)) Settings.Default.ShowConsole = value; }
        }

        // ---- Update state (hub + firmware) ----
        private UpdateInfo updateInfo;
        public UpdateInfo UpdateInfo
        {
            get => updateInfo;
            private set
            {
                if (SetField(ref updateInfo, value))
                {
                    OnPropertyChanged(nameof(UpdateAvailable));
                    OnPropertyChanged(nameof(FirmwareUpdateAvailable));
                    OnPropertyChanged(nameof(UpdateBadgeText));
                }
            }
        }

        public bool UpdateAvailable
        {
            get
            {
                if (updateInfo == null || !updateInfo.HubAvailable) return false;
                var skipped = Settings.Default.SkipVersion;
                if (!string.IsNullOrEmpty(skipped)
                    && updateInfo.LatestVersion != null
                    && string.Equals(skipped, updateInfo.LatestVersion.ToString(), StringComparison.Ordinal))
                {
                    return false;
                }
                return true;
            }
        }

        public bool FirmwareUpdateAvailable
        {
            get
            {
                if (updateInfo == null || updateInfo.FirmwareVersion == null) return false;
                if (firmwareVersion == null) return false;
                return updateInfo.FirmwareVersion.CompareTo(firmwareVersion) > 0;
            }
        }

        public string UpdateBadgeText
        {
            get
            {
                if (updateInfo == null || !updateInfo.HubAvailable) return string.Empty;
                return "Update " + (updateInfo.LatestVersion != null ? "v" + updateInfo.LatestVersion : "available");
            }
        }

        private string firmwareVersionText;
        public string FirmwareVersionText
        {
            get => firmwareVersionText;
            private set { if (SetField(ref firmwareVersionText, value)) OnPropertyChanged(nameof(FirmwareUpdateAvailable)); }
        }

        private Version firmwareVersion;
        public Version FirmwareVersion
        {
            get => firmwareVersion;
            private set
            {
                if (SetField(ref firmwareVersion, value))
                {
                    OnPropertyChanged(nameof(FirmwareUpdateAvailable));
                }
            }
        }

        private bool isFlashingFirmware;
        public bool IsFlashingFirmware
        {
            get => isFlashingFirmware;
            private set
            {
                if (SetField(ref isFlashingFirmware, value))
                {
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public void SetUpdateInfo(UpdateInfo info)
        {
            UpdateInfo = info;
            if (info != null && info.HubAvailable)
            {
                Log(LogKind.Info, "Update available: v" + info.LatestVersion);
            }
        }

        public string CurrentAppVersion => GetAppVersion();

        public string CurrentFirmwareVersionDisplay =>
            firmwareVersion != null ? "v" + firmwareVersion.ToString() : (firmwareVersionText ?? "—");

        private string accentColor = "#E5484D";
        public string AccentColor
        {
            get => accentColor;
            set
            {
                if (SetField(ref accentColor, value))
                {
                    Settings.Default.AccentColor = value;
                    (System.Windows.Application.Current as App)?.ApplyAccent(value);
                }
            }
        }

        public event EventHandler LogAppended;
        public event EventHandler LogCleared;

        public RelayCommand ConnectCommand { get; }
        public RelayCommand DisconnectCommand { get; }
        public RelayCommand AutoDetectCommand { get; }
        public RelayCommand RefreshPortsCommand { get; }
        public RelayCommand MoveCommand { get; }
        public RelayCommand HaltCommand { get; }
        public RelayCommand JogCommand { get; }
        public RelayCommand SetZeroCommand { get; }
        public RelayCommand CalibrateCommand { get; }
        public RelayCommand SetLimitCommand { get; }
        public RelayCommand ClearLogCommand { get; }
        public RelayCommand CopyLogCommand { get; }
        public RelayCommand TargetIncrementCommand { get; }
        public RelayCommand TargetDecrementCommand { get; }

        public MainViewModel()
        {
            uiDispatcher = Dispatcher.CurrentDispatcher;

            serial = new SerialManager();
            pipes = new PipeServer(serial);

            serial.SerialTraffic += OnSerialTraffic;
            serial.ConnectionStateChanged += OnConnectionStateChanged;
            pipes.ClientCountChanged += OnPipeClientCountChanged;

            ConnectCommand = new RelayCommand(_ => ConnectAsync().ConfigureAwait(false), _ => !isConnected);
            DisconnectCommand = new RelayCommand(_ => DisconnectAsync().ConfigureAwait(false), _ => isConnected);
            AutoDetectCommand = new RelayCommand(_ => AutoDetectAsync().ConfigureAwait(false), _ => !isConnected);
            RefreshPortsCommand = new RelayCommand(_ => RefreshPorts(), _ => !isConnected);
            MoveCommand = new RelayCommand(_ => MoveAsync(target).ConfigureAwait(false),
                                            _ => isConnected && !isMoving && !isCalibrating);
            HaltCommand = new RelayCommand(_ => Halt(), _ => isConnected && (isMoving || isCalibrating));
            JogCommand = new RelayCommand(p => JogAsync(p).ConfigureAwait(false),
                                           _ => isConnected && !isMoving && !isCalibrating);
            SetZeroCommand = new RelayCommand(_ => SetZero(), _ => isConnected && !isMoving && !isCalibrating);
            CalibrateCommand = new RelayCommand(_ => CalibrateAsync().ConfigureAwait(false),
                                                 _ => isConnected && !isMoving && !isCalibrating);
            SetLimitCommand = new RelayCommand(_ => SetLimit(), _ => isConnected && isCalibrating);
            ClearLogCommand = new RelayCommand(_ => ClearLog());
            CopyLogCommand = new RelayCommand(_ => CopyLog());
            TargetIncrementCommand = new RelayCommand(_ => Target = Math.Min(maxPosition <= 0 ? int.MaxValue : maxPosition, target + stepSize));
            TargetDecrementCommand = new RelayCommand(_ => Target = Math.Max(0, target - stepSize));

            pollTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            pollTimer.Tick += (s, e) => PollDevice();

            backlashCompensation = Settings.Default.BacklashCompensation;
            autoConnectOnOpen = Settings.Default.AutoConnectOnStartup;
            connectionExpanded = Settings.Default.ConnectionExpanded;
            settingsExpanded = Settings.Default.SettingsExpanded;
            positionExpanded = Settings.Default.PositionExpanded;
            movementExpanded = Settings.Default.MovementExpanded;
            showProgress = Settings.Default.ShowProgress;
            showConsole = Settings.Default.ShowConsole;
            accentColor = Settings.Default.AccentColor ?? "#E5484D";

            // Capture the *persisted* last port BEFORE any UI default-selection
            // mutates it. This is the only port we'll consider auto-connecting to.
            persistedLastPort = Settings.Default.LastComPort;

            RefreshPorts();

            if (!string.IsNullOrEmpty(persistedLastPort) && AvailablePorts.Contains(persistedLastPort))
            {
                SelectedPort = persistedLastPort;
            }
            else if (AvailablePorts.Count > 0)
            {
                SelectedPort = AvailablePorts[0];
            }

            Log(LogKind.Info, "DeFocuser Controller v" + GetAppVersion() + " ready");
        }

        private readonly string persistedLastPort;

        public void TryAutoConnectOnStartup()
        {
            if (!autoConnectOnOpen) return;
            // Only auto-connect if a port was *previously persisted* by a
            // successful prior connect — never on a port the constructor
            // auto-selected as a UI default.
            if (string.IsNullOrEmpty(persistedLastPort)) return;
            if (!AvailablePorts.Contains(persistedLastPort)) return;

            SelectedPort = persistedLastPort;
            ConnectAsync().ConfigureAwait(false);
        }

        public void RefreshPorts()
        {
            string current = selectedPort;
            AvailablePorts.Clear();
            try
            {
                foreach (var p in SerialPort.GetPortNames().OrderBy(s => s))
                    AvailablePorts.Add(p);
            }
            catch (Exception ex)
            {
                Log(LogKind.Err, "Port enumeration failed: " + ex.Message);
            }

            if (current != null && AvailablePorts.Contains(current))
                SelectedPort = current;
            else if (AvailablePorts.Count > 0)
                SelectedPort = AvailablePorts[0];
            else
                SelectedPort = null;

            Log(LogKind.Info, "Refreshed ports (" + AvailablePorts.Count + " found)");
        }

        private async Task ConnectAsync()
        {
            string portToUse = autoDetect ? null : selectedPort;
            bool useAuto = autoDetect;

            Log(LogKind.Info, useAuto ? "Auto-detecting DeFocuser..." : ("Opening " + portToUse + " @ " + selectedBaud));

            try
            {
                await Task.Run(() => serial.Connect(portToUse, useAuto));
                pipes.Start();

                Settings.Default.LastComPort = serial.ConnectedPortName;
                Settings.Default.Save();

                IsConnected = true;
                Log(LogKind.Ok, "Connected on " + serial.ConnectedPortName);

                await RefreshDeviceStateAsync();
                pollTimer.Start();
            }
            catch (Exception ex)
            {
                IsConnected = false;
                Log(LogKind.Err, "Connection failed: " + ex.Message);
                MessageBox.Show("Failed to connect: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task AutoDetectAsync()
        {
            AutoDetect = true;
            await ConnectAsync();
        }

        private async Task DisconnectAsync()
        {
            if (pipes.ConnectedClientCount > 0)
            {
                var result = MessageBox.Show(
                    "There are " + pipes.ConnectedClientCount + " ASCOM client(s) still connected. Disconnecting will break their connection. Continue?",
                    "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;
            }

            pollTimer.Stop();

            try
            {
                pipes.Stop();
                await Task.Run(() => serial.Disconnect());
                IsConnected = false;
                IsMoving = false;
                IsCalibrating = false;
                Log(LogKind.Info, "Disconnected");
            }
            catch (Exception ex)
            {
                Log(LogKind.Err, "Disconnect error: " + ex.Message);
            }
        }

        private async Task RefreshDeviceStateAsync()
        {
            try
            {
                int p = await Task.Run(() => serial.GetPosition());
                int m = await Task.Run(() => serial.GetMaxPosition());
                bool rev = await Task.Run(() => serial.GetIsReverse());
                string spd = await Task.Run(() => SafeGetSpeed());
                int? thr = await Task.Run(() => SafeGetStallThreshold());
                string info = await Task.Run(() => SafeGetFirmwareInfo());

                if (!string.IsNullOrEmpty(info))
                {
                    FirmwareVersionText = info;
                    FirmwareVersion = ParseFirmwareVersion(info);
                }

                Position = p;
                MaxPosition = m;
                Target = p;
                reverse = rev;
                OnPropertyChanged(nameof(Reverse));

                if (!string.IsNullOrEmpty(spd))
                {
                    speed = NormalizeSpeed(spd);
                    OnPropertyChanged(nameof(Speed));
                }

                if (thr.HasValue)
                {
                    stallThreshold = Math.Max(StallThresholdMin, Math.Min(StallThresholdMax, thr.Value));
                    OnPropertyChanged(nameof(StallThreshold));
                }
            }
            catch (Exception ex)
            {
                Log(LogKind.Err, "Initial state read failed: " + ex.Message);
            }
        }

        private string SafeGetSpeed()
        {
            try { return serial.GetSpeed(); }
            catch { return null; }
        }

        private static string GetAppVersion()
        {
            try
            {
                var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                if (v == null) return "?";
                // Trim trailing .0 revision (matches the X.Y.Z tag passed to build.ps1).
                return v.Revision == 0
                    ? v.Major + "." + v.Minor + "." + v.Build
                    : v.ToString();
            }
            catch { return "?"; }
        }

        private int? SafeGetStallThreshold()
        {
            try { return serial.GetStallThreshold(); }
            catch { return null; }
        }

        private string SafeGetFirmwareInfo()
        {
            try { return serial.GetFirmwareInfo(); }
            catch { return null; }
        }

        private static readonly Regex firmwareVersionRegex = new Regex(@"v(\d+\.\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);

        private static Version ParseFirmwareVersion(string infoLine)
        {
            if (string.IsNullOrEmpty(infoLine)) return null;
            var m = firmwareVersionRegex.Match(infoLine);
            if (!m.Success) return null;
            return UpdateChecker.TryParse(m.Groups[1].Value);
        }

        private static string NormalizeSpeed(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Fast";
            string u = raw.Trim().ToUpperInvariant();
            if (u == "FAST") return "Fast";
            if (u == "NORMAL") return "Normal";
            if (u == "SLOW") return "Slow";
            return "Fast";
        }

        private void PollDevice()
        {
            if (!isConnected) return;

            Task.Run(() =>
            {
                try
                {
                    int p = serial.GetPosition();
                    bool moving = serial.GetIsMoving();
                    bool calib = serial.GetIsCalibrating();
                    int m = serial.GetMaxPosition();
                    uiDispatcher.BeginInvoke(new Action(() =>
                    {
                        Position = p;
                        MaxPosition = m;
                        IsMoving = moving;
                        IsCalibrating = calib;
                    }));
                }
                catch
                {
                    // Suppress polling errors; the next tick will retry.
                }
            });
        }

        private async Task MoveAsync(int destination)
        {
            if (!isConnected) return;

            int current;
            try { current = serial.GetPosition(); }
            catch (Exception ex) { Log(LogKind.Err, "Read position failed: " + ex.Message); return; }

            int delta = destination - current;
            int backlash = backlashCompensation;

            try
            {
                if (delta > 0 && backlash > 0)
                {
                    await Task.Run(() => serial.Move(current + backlash + delta));
                    IsMoving = true;
                    Log(LogKind.Send, "MOVE " + (current + backlash + delta) + " (+backlash overshoot)");
                    await WaitForMoveStop();

                    int newPos = serial.GetPosition();
                    await Task.Run(() => serial.Move(newPos - backlash));
                    IsMoving = true;
                    Log(LogKind.Send, "MOVE " + (newPos - backlash) + " (backlash retract)");
                    await WaitForMoveStop();
                }
                else
                {
                    await Task.Run(() => serial.Move(destination));
                    IsMoving = true;
                    Log(LogKind.Send, "MOVE " + destination);
                    await WaitForMoveStop();
                }
            }
            catch (Exception ex)
            {
                Log(LogKind.Err, "Move failed: " + ex.Message);
                MessageBox.Show("Error moving focuser: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task WaitForMoveStop()
        {
            await Task.Run(() =>
            {
                while (true)
                {
                    bool moving;
                    int p;
                    try
                    {
                        moving = serial.GetIsMoving();
                        p = serial.GetPosition();
                    }
                    catch { break; }

                    uiDispatcher.BeginInvoke(new Action(() =>
                    {
                        Position = p;
                        IsMoving = moving;
                    }));

                    if (!moving) break;
                    System.Threading.Thread.Sleep(300);
                }
            });
        }

        private async Task JogAsync(object parameter)
        {
            if (!isConnected) return;

            int multiplier = 1;
            if (parameter is string s)
            {
                if (!int.TryParse(s, out multiplier)) multiplier = 1;
            }
            else if (parameter is int i) multiplier = i;

            int delta = stepSize * multiplier;
            int destination = position + delta;
            if (maxPosition > 0) destination = Math.Min(maxPosition, Math.Max(0, destination));
            else destination = Math.Max(0, destination);

            Target = destination;
            await MoveAsync(destination);
        }

        private void Halt()
        {
            try
            {
                serial.Halt();
                Log(LogKind.Warn, "HALT issued");
            }
            catch (Exception ex)
            {
                Log(LogKind.Err, "Halt failed: " + ex.Message);
            }
        }

        private void SetZero()
        {
            try
            {
                serial.SetZeroPosition();
                Log(LogKind.Ok, "Zero position set");
            }
            catch (Exception ex)
            {
                Log(LogKind.Err, "SetZero failed: " + ex.Message);
                MessageBox.Show("Error: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task CalibrateAsync()
        {
            try
            {
                await Task.Run(() => serial.Calibrate());
                IsCalibrating = true;
                Log(LogKind.Info, "Calibration started — use Set Limit to advance");

                await Task.Run(() =>
                {
                    while (true)
                    {
                        bool calib;
                        int p;
                        try
                        {
                            calib = serial.GetIsCalibrating();
                            p = serial.GetPosition();
                        }
                        catch { break; }

                        uiDispatcher.BeginInvoke(new Action(() =>
                        {
                            Position = p;
                            IsCalibrating = calib;
                        }));

                        if (!calib) break;
                        System.Threading.Thread.Sleep(500);
                    }
                });

                Log(LogKind.Ok, "Calibration complete");
            }
            catch (Exception ex)
            {
                Log(LogKind.Err, "Calibration error: " + ex.Message);
                MessageBox.Show("Calibration error: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetLimit()
        {
            try
            {
                serial.SetLimit();
                Log(LogKind.Ok, "Limit marker advanced");
            }
            catch (Exception ex)
            {
                Log(LogKind.Err, "SetLimit failed: " + ex.Message);
                MessageBox.Show("Set Limit error: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearLog()
        {
            LogEntries.Clear();
            LogCleared?.Invoke(this, EventArgs.Empty);
        }

        private void CopyLog()
        {
            try
            {
                var text = string.Join(Environment.NewLine,
                    LogEntries.Select(e => e.Timestamp + " [" + e.KindLabel + "] " + e.Message));
                if (!string.IsNullOrEmpty(text))
                    Clipboard.SetText(text);
            }
            catch (Exception ex)
            {
                Log(LogKind.Err, "Copy failed: " + ex.Message);
            }
        }

        public void Log(LogKind kind, string message)
        {
            if (!logEnabled) return;
            var entry = new LogEntry(kind, message);
            if (uiDispatcher.CheckAccess())
            {
                AppendLog(entry);
            }
            else
            {
                uiDispatcher.BeginInvoke(new Action(() => AppendLog(entry)));
            }
        }

        private void AppendLog(LogEntry entry)
        {
            LogEntries.Add(entry);
            while (LogEntries.Count > 500) LogEntries.RemoveAt(0);
            LogAppended?.Invoke(this, EventArgs.Empty);
        }

        private void OnSerialTraffic(object sender, SerialTrafficEventArgs e)
        {
            var kind = e.Direction == SerialDirection.Tx ? LogKind.Send : LogKind.Recv;
            Log(kind, e.Payload);
        }

        private void OnConnectionStateChanged(bool connected)
        {
            if (uiDispatcher.CheckAccess())
                IsConnected = connected;
            else
                uiDispatcher.BeginInvoke(new Action(() => IsConnected = connected));
        }

        private int lastClientCount;
        private void OnPipeClientCountChanged(int count)
        {
            if (count != lastClientCount)
            {
                Log(LogKind.Info, "ASCOM clients: " + count);
                lastClientCount = count;
            }
        }

        public bool HasAscomClients => pipes.ConnectedClientCount > 0;
        public int AscomClientCount => pipes.ConnectedClientCount;

        // Flashes the ESP32-C3 over the same serial port the hub uses. The hub
        // must release the port for esptool — caller should have warned the
        // user about any connected ASCOM clients before calling.
        public async Task<bool> FlashFirmwareAsync(
            string firmwareUrl,
            Version firmwareVersion,
            CancellationToken ct)
        {
            if (isFlashingFirmware) return false;

            string esptool = FirmwareFlasher.ResolveEsptoolPath();
            if (esptool == null)
            {
                Log(LogKind.Err, "esptool.exe not found. Reinstall the hub to restore it.");
                return false;
            }

            string port = serial.ConnectedPortName ?? Settings.Default.LastComPort;
            if (string.IsNullOrEmpty(port))
            {
                Log(LogKind.Err, "No COM port available for firmware flash.");
                return false;
            }

            IsFlashingFirmware = true;
            bool wasConnected = isConnected;
            try
            {
                Log(LogKind.Info, "Downloading firmware v" + firmwareVersion + " ...");
                string binPath = await FirmwareFlasher.DownloadFirmwareAsync(firmwareUrl, firmwareVersion, ct);
                Log(LogKind.Ok, "Firmware downloaded (" + new System.IO.FileInfo(binPath).Length + " bytes)");

                if (wasConnected)
                {
                    Log(LogKind.Info, "Releasing serial port " + port + " for flash");
                    pollTimer.Stop();
                    pipes.Stop();
                    await Task.Run(() => serial.Disconnect());
                    IsConnected = false;
                    await Task.Delay(500, ct);
                }

                Log(LogKind.Info, "Flashing " + port + " via esptool...");
                bool ok = await FirmwareFlasher.FlashAsync(esptool, port, binPath,
                    line => Log(line.IsError ? LogKind.Warn : LogKind.Recv, "esptool: " + line.Line),
                    ct);

                if (ok)
                {
                    Log(LogKind.Ok, "Flash complete. Waiting for chip reboot...");
                    await Task.Delay(2000, ct);
                }
                else
                {
                    Log(LogKind.Err, "Flash failed. Firmware on device may be in an inconsistent state.");
                }

                if (wasConnected)
                {
                    Log(LogKind.Info, "Reconnecting to " + port);
                    SelectedPort = port;
                    AutoDetect = false;
                    await ConnectAsync();
                }

                return ok;
            }
            catch (OperationCanceledException)
            {
                Log(LogKind.Warn, "Firmware flash cancelled");
                return false;
            }
            catch (Exception ex)
            {
                Log(LogKind.Err, "Firmware flash error: " + ex.Message);
                return false;
            }
            finally
            {
                IsFlashingFirmware = false;
            }
        }

        public void Dispose()
        {
            pollTimer?.Stop();
            try { pipes?.Dispose(); } catch { }
            try { serial?.Dispose(); } catch { }
        }
    }
}
