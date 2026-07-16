using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using VMUpdater.Helpers;
using VMUpdater.Views;

namespace VMUpdater.ViewModels
{
    public partial class MainViewModel : ViewModelBase
    {
        private readonly string _logFilePath;
        private bool _isLoading;
        private static bool IsInDesignMode => DesignerProperties.GetIsInDesignMode(new DependencyObject());
        public Action<string>? OnTooltipRefreshRequested { get; set; }
        public ObservableCollection<string> DaysOfWeek { get; } = [
            "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"
        ];

        #region Properties
        private string _selectedDay = "Sunday";
        public string SelectedDay
        {
            get => _selectedDay;
            set
            {
                if (SetProperty(ref _selectedDay, value, nameof(SelectedDay)))
                { 
                    CalculateNextScheduledUpdate();
                    SaveProfileConfigurationSilently();
                }
            }
        }

        private DateTime? _selectedTime = DateTime.Now;
        public DateTime? SelectedTime
        {
            get => _selectedTime;
            set
            {
                if (SetProperty(ref _selectedTime, value, nameof(SelectedTime)))
                {
                    CalculateNextScheduledUpdate();
                    SaveProfileConfigurationSilently();
                }
            }
        }

        private string _username = string.Empty;
        public string Username
        {
            get => _username;
            set
            {
                if (SetProperty(ref _username, value, nameof(Username)))
                {
                    SaveProfileConfigurationSilently();
                }
            }
        }

        private string _password = string.Empty;
        public string Password
        {
            get => _password;
            set
            {
                if (SetProperty(ref _password, value, nameof(Password)))
                {
                    SaveProfileConfigurationSilently();
                }
            }
        }

        private string _vmxPath = string.Empty;
        public string VMXPath
        {
            get => _vmxPath;
            set
            {
                if (SetProperty(ref _vmxPath, value, nameof(VMXPath)))
                {
                    SaveProfileConfigurationSilently();
                }
            }
        }

        private DateTime _lastSuccessfulUpdate = DateTime.MinValue;
        public DateTime LastSuccessfulUpdate
        {
            get => _lastSuccessfulUpdate;
            set
            {
                if (SetProperty(ref _lastSuccessfulUpdate, value, nameof(LastSuccessfulUpdate)))
                {
                    OnPropertyChanged(nameof(TrayToolTipText));
                    OnTooltipRefreshRequested?.Invoke(TrayToolTipText);
                }
            }
        }
        
        public string TrayToolTipText
        {
            get
            {
                string statusText = IsUpdating 
                    ? "Updating..." 
                    : (LastSuccessfulUpdate == DateTime.MinValue
                        ? "Last Update: Never" 
                        : $"Last Update: {LastSuccessfulUpdate:dddd, MMMM d}");

                return $"VMUpdater\n{statusText}";
            }
        }

        private DateTime _nextUpdate = DateTime.MinValue;
        public DateTime NextUpdate
        {
            get => _nextUpdate;
            set => SetProperty(ref _nextUpdate, value, nameof(NextUpdate));
        }

        private string _logText = string.Empty;
        public string LogText
        {
            get => _logText;
            set => SetProperty(ref _logText, value, nameof(LogText));
        }

        private string _statusMessage = "Ready.";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value, nameof(StatusMessage));
        }

        private double _updateProgress = 0.0;
        public double UpdateProgress
        {
            get => _updateProgress;
            set => SetProperty(ref _updateProgress, value, nameof(UpdateProgress));
        }

        private bool _isUpdating = false;
        public bool IsUpdating
        {
            get => _isUpdating;
            set
            {
                if (SetProperty(ref _isUpdating, value, nameof(IsUpdating)))
                {
                    OnPropertyChanged(nameof(TrayToolTipText));
                    OnTooltipRefreshRequested?.Invoke(TrayToolTipText);
                }
            }
        }
        #endregion

        #region Commands
        public ICommand StartUpdateCommand { get; }
        public ICommand StartForceUpdateCommand { get; }
        public ICommand BrowseFolderCommand { get; }
        public ICommand ShowMainWindowCommand { get; }
        public ICommand ShowLogCommand { get; }
        public ICommand ExitCommand { get; }
        #endregion

        public MainViewModel()
        {
            if (IsInDesignMode)
            {
                // Set up mock data so the designer looks pretty!
                SelectedDay = "Monday";
                VMXPath = @"Path/To/VMX.vmx";
                Username = "username";
                Password = "password";
                return;
            }

            // Setup File Logging Directory matching your WinForms initialization
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            if (!Directory.Exists(logFolder)) 
                Directory.CreateDirectory(logFolder);
            _logFilePath = Path.Combine(logFolder, $"{timestamp}.log");

            // Commands Configuration
            StartUpdateCommand      =   new RelayCommand(execute: async () => await ExecuteStartUpdate(forceUpdate: false));
            StartForceUpdateCommand =   new RelayCommand(execute: async () => await ExecuteStartUpdate(forceUpdate: true));
            BrowseFolderCommand     =   new RelayCommand(execute: BrowseForVMXFile);
            ShowMainWindowCommand   =   new RelayCommand(execute: ExecuteShowMainWindow);
            ShowLogCommand          =   new RelayCommand(execute: ExecuteShowLog);
            ExitCommand             =   new RelayCommand(execute: () => Application.Current.Shutdown());

            LoadProfileConfiguration();
            LogMessage("Logging profile initialized.");
        }

        private void ExecuteShowMainWindow()
        {
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow != null)
            {
                mainWindow.Show();
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Activate();
            }
        }

        private void ExecuteShowLog()
        {
            var window = Application.Current.MainWindow;
            if (window is MainWindow mainWindow)
            {
                mainWindow.Show();
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.MainTabControl.SelectedIndex = 1;
                mainWindow.Activate();
            }
        }

        public void LogMessage(string message)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string formatLine = $"[{timestamp}] {message}{Environment.NewLine}";
                File.AppendAllText(_logFilePath, formatLine);

                // Append directly to the bound control string
                LogText += formatLine;
            }
            catch { /* Prevent filesystem context lock crashes */ }
        }

        private void CalculateNextScheduledUpdate()
        {
            if (string.IsNullOrEmpty(SelectedDay) || !SelectedTime.HasValue) return;

            if (Enum.TryParse(SelectedDay, true, out DayOfWeek targetDay))
            {
                DateTime now = DateTime.Now;
                DateTime timeTarget = SelectedTime.Value;
                DateTime calculated = new(now.Year, now.Month, now.Day, timeTarget.Hour, timeTarget.Minute, 0);

                while (calculated.DayOfWeek != targetDay)
                {
                    calculated = calculated.AddDays(1);
                }

                if (calculated < now)
                {
                    calculated = calculated.AddDays(7);
                }

                NextUpdate = calculated;
            }
        }

        private void BrowseForVMXFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "VMware Configuration (*.vmx)|*.vmx",
                Title = "Select Virtual Machine VMX Configuration Target File"
            };

            if (dialog.ShowDialog() == true) VMXPath = dialog.FileName;
        }

        private void LoadProfileConfiguration()
        {
            if (IsInDesignMode || Properties.Settings.Default == null) return;
            _isLoading = true; // Block saves during initialization
            try
            {
                // Wire your properties to your global persistent app context model
                VMXPath = Properties.Settings.Default.vmxPath;
                Username = Properties.Settings.Default.vmUsername;
                Password = Properties.Settings.Default.vmPassword;
                SelectedDay = !string.IsNullOrEmpty(Properties.Settings.Default.scheduleDay) ? Properties.Settings.Default.scheduleDay : "Sunday";

                if (Properties.Settings.Default.scheduleTime != DateTime.MinValue)
                    SelectedTime = Properties.Settings.Default.scheduleTime;

                LastSuccessfulUpdate = Properties.Settings.Default.LastUpdateRan;
                CalculateNextScheduledUpdate();
            }
            finally
            {
                _isLoading = false; // Re-enable saving for normal UI operations
            }
        }

        /// <summary>
        /// Saves changes silently to user settings during real-time UI manipulation without flooding the execution logs.
        /// </summary>
        private void SaveProfileConfigurationSilently()
        {
            if (_isLoading) return;

            Properties.Settings.Default.vmxPath = VMXPath;
            Properties.Settings.Default.vmUsername = Username;
            Properties.Settings.Default.vmPassword = Password;
            Properties.Settings.Default.scheduleDay = SelectedDay;
            Properties.Settings.Default.scheduleTime = SelectedTime ?? DateTime.MinValue;
            Properties.Settings.Default.Save();
        }

        public async Task ExecuteStartUpdate(bool forceUpdate = false)
        {
            if (IsUpdating) return;
            if (forceUpdate) LogMessage("User started manual update.");

            string vmrunPath = @"C:\Program Files (x86)\VMware\VMware Workstation\vmrun.exe";
            IsUpdating = true;
            UpdateProgress = 10;
            LogMessage("Initializing automated execution loop headlessly...");
            StatusMessage = "Starting update process...";

            bool executionSucceeded = false;

            try
            {
                // Step 1: Headless Invocation
                await RunProcessAsync(vmrunPath, $"-T ws start \"{VMXPath}\" nogui");
                UpdateProgress = 25;

                // Step 2: Static boot wait
                LogMessage("Allowing 45-second stabilization period for system kernel guest components...");
                StatusMessage = "Stabilizing system components...";
                await Task.Delay(45000);
                UpdateProgress = 50;

                // Step 3: Network Check
                LogMessage("Checking outward-bound routing connection from guest adapter...");
                StatusMessage = "Performing network check...";
                string pingArgs = $"-T ws -gu \"{Username}\" -gp \"{Password}\" runScriptInGuest \"{VMXPath}\" /bin/bash \"ping -c 3 8.8.8.8\"";
                UpdateProgress = 60;
                int pingCode = await RunProcessAsync(vmrunPath, pingArgs);

                if (pingCode != 0)
                {
                    LogMessage($"Abort: Intermittent network ping test rejected execution with exit frame code: {pingCode}");
                    StatusMessage = "Aborted: Network connectivity validation failed.";
                    return;
                }

                // Step 4: Upgrade Transaction Execution
                LogMessage("Updating Arch keyring to prevent signature issues...");
                StatusMessage = "Updating keyrings...";
                // Update keyring first to avoid signature errors
                string keyringArgs = $"-T ws -gu \"{Username}\" -gp \"{Password}\" runScriptInGuest \"{VMXPath}\" /bin/bash \"echo '{Password}' | sudo -S pacman -Sy --noconfirm archlinux-keyring\"";
                await RunProcessAsync(vmrunPath, keyringArgs);

                LogMessage("Sending package transaction orders to guest system...");
                StatusMessage = "Executing package transactions...";

                string pacmanArgs = $"-T ws -gu \"{Username}\" -gp \"{Password}\" runScriptInGuest \"{VMXPath}\" /bin/bash \"yay -Syu --noconfirm\"";

                UpdateProgress = 75;
                int upgradeCode = await RunProcessAsync(vmrunPath, pacmanArgs);
            }
            catch (Exception ex)
            {
                LogMessage($"Fatal Processing Exception: {ex.Message}");
                StatusMessage = "Update process encountered a fatal error.";
            }
            finally
            {
                LogMessage("Terminating hypervisor profile gracefully...");
                await RunProcessAsync(vmrunPath, $"-T ws stop \"{VMXPath}\" soft");

                if (executionSucceeded)
                {
                    UpdateProgress = 100;
                    // Let the user see the 100% full green bar and success message for 2 seconds
                    await Task.Delay(2000);
                }

                // Clean up UI states
                IsUpdating = false;
                UpdateProgress = 0;

                if (StatusMessage == "Starting update process..." || StatusMessage == "Terminating hypervisor profile...")
                    StatusMessage = "Ready.";

                if (!forceUpdate)
                    CalculateNextScheduledUpdate();
            }
        }

        private Task<int> RunProcessAsync(string fileName, string arguments)
        {
            var tcs = new TaskCompletionSource<int>();
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(fileName),
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true
            };

            var process = new System.Diagnostics.Process { StartInfo = startInfo, EnableRaisingEvents = true };

            process.Exited += (s, e) =>
            {
                string err = process.StandardError.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(err)) LogMessage($"[Process StandardError Stream Output]: {err.Trim()}");
                tcs.SetResult(process.ExitCode);
                process.Dispose();
            };

            try { process.Start(); }
            catch (Exception ex) { LogMessage($"Process failed start parameters initialization: {ex.Message}"); tcs.SetResult(-1); }

            return tcs.Task;
        }
    }
}