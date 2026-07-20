using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using VMUpdater.Helpers;
using VMUpdater.Models;
using VMUpdater.Services;
using VMUpdater.Views;

namespace VMUpdater.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly string _logFilePath;
        private readonly VirtualMachineService _vmService;
        public Action<string>? OnTooltipRefreshRequested { get; set; }
        public ObservableCollection<VirtualMachineViewModel> VirtualMachines { get; }

        public MainViewModel()
        {
            _vmService = new();
            VirtualMachines = [];
            string logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            if (!Directory.Exists(logFolder))
                Directory.CreateDirectory(logFolder);
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            _logFilePath = Path.Combine(logFolder, $"{timestamp}.log");

            ShowMainWindowCommand = new RelayCommand(execute: ExecuteShowMainWindow);
            ShowLogCommand = new RelayCommand(execute: ExecuteShowLog);
            AddVirtualMachineCommand = new RelayCommand<string>(AddVirtualMachine);
            RemoveVirtualMachineCommand = new RelayCommand<VirtualMachineViewModel>(RemoveVirtualMachine);
            BrowseForVMWareExecutableCommand = new RelayCommand(BrowseForVMWareExecutable);
            BrowseForVirtualBoxExecutableCommand = new RelayCommand(BrowseForVirtualBoxExecutable);
            BrowseForQEMUExecutableCommand = new RelayCommand(BrowseForQEMUExecutable);
            ExitCommand = new RelayCommand(() => Application.Current.Shutdown());

            InitializeApplicationProfiles();
            LogMessage("Logging profile initialized.");
        }

        #region Properties
        private double _updateProgress = 0.0;
        private bool _isUpdating = false;
        private string _logText = string.Empty;
        private string _statusMessage = "Ready.";
        public double UpdateProgress { get => _updateProgress; set => SetProperty(ref _updateProgress, value, nameof(UpdateProgress)); }
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
        public string TrayToolTipText
        {
            get
            {
                string statusText = IsUpdating ? "Updating..." : $"All VMs Updated.";

                return $"VMUpdater\n{statusText}";
            }
        }
        public string LogText { get => _logText; set => SetProperty(ref _logText, value, nameof(LogText)); }
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value, nameof(StatusMessage)); }
        public string VMWareExecutablePath
        {
            get => Properties.Settings.Default.VMRunPath;
            set
            {
                if (SetProperty(() => Properties.Settings.Default.VMRunPath, val => Properties.Settings.Default.VMRunPath = val, value))
                    Properties.Settings.Default.Save();
            }
        }
        public string VirtualBoxExecutablePath
        {
            get => Properties.Settings.Default.VBoxManagePath;
            set
            {
                if (SetProperty(() => Properties.Settings.Default.VBoxManagePath, val => Properties.Settings.Default.VBoxManagePath = val, value))
                    Properties.Settings.Default.Save();
            }
        }
        public string QEMUExecutablePath
        {
            get => Properties.Settings.Default.QEMUExecutablePath;
            set
            {
                if (SetProperty(() => Properties.Settings.Default.QEMUExecutablePath, val => Properties.Settings.Default.QEMUExecutablePath = val, value))
                    Properties.Settings.Default.Save();
            }
        }
        #endregion

        #region Commands
        public ICommand ShowMainWindowCommand { get; }
        public ICommand ShowLogCommand { get; }
        public ICommand AddVirtualMachineCommand { get; }
        public ICommand RemoveVirtualMachineCommand { get; }
        public ICommand BrowseForVMWareExecutableCommand { get; }
        public ICommand BrowseForVirtualBoxExecutableCommand { get; }
        public ICommand BrowseForQEMUExecutableCommand { get; }
        public ICommand ExitCommand { get; }
        #endregion

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

        private void AddVirtualMachine(string? hypervisorType)
        {
            var newModel = new VirtualMachineModel
            {
                VMPath = string.Empty,
                Username = string.Empty,
                Password = string.Empty,
                ScheduleDay = "Monday",
                ScheduleTime = DateTime.Now
            };

            if (hypervisorType!.Equals("VMWare", StringComparison.OrdinalIgnoreCase))
                newModel.Hypervisor = HypervisorType.VMWare;
            else if (hypervisorType.Equals("VirtualBox", StringComparison.OrdinalIgnoreCase))
                newModel.Hypervisor = HypervisorType.VirtualBox;
            else if (hypervisorType.Equals("QEMU", StringComparison.OrdinalIgnoreCase))
                newModel.Hypervisor = HypervisorType.QEMU;
            else
            {
                LogMessage($"Unknown hypervisor type: {hypervisorType}. Defaulting to VMWare.");
                newModel.Hypervisor = HypervisorType.VMWare;
            }

            var newItemViewModel = new VirtualMachineViewModel(newModel, _vmService, CollapseSiblings) { IsExpanded = true };

            newItemViewModel.RequestStartUpdate += async (vm, forceUpdate) => await ExecuteStartUpdate(vm, forceUpdate);

            VirtualMachines.Add(newItemViewModel);
        }
        private void RemoveVirtualMachine(VirtualMachineViewModel? itemToRemove)
        {
            if (itemToRemove != null)
            {
                VirtualMachines.Remove(itemToRemove);
                _vmService.DeleteVirtualMachineEntry(itemToRemove.Model);
            }
        }
        private void CollapseSiblings(VirtualMachineViewModel expandedItem)
        {
            foreach (var vm in VirtualMachines.Where(vm => vm != expandedItem))
                vm.IsExpanded = false;
        }
        public async Task ExecuteStartUpdate(VirtualMachineViewModel vm, bool forceUpdate = false)
        {
            if (IsUpdating) return;
            if (forceUpdate) LogMessage("User started manual update.");

            IsUpdating = true;
            UpdateProgress = 10;
            StatusMessage = "Starting...";

            try
            {
                // Invoke service layer passing functional callback hooks 
                await _vmService!.StartUpdateAsync(
                    vm.Model,
                    report => Application.Current.Dispatcher.Invoke(() =>
                    {
                        // This safely executes on the UI thread
                        if (report.ProgressDelta > 0) UpdateProgress = report.ProgressDelta;
                        if (!string.IsNullOrEmpty(report.StatusText)) StatusMessage = report.StatusText;
                        if (!string.IsNullOrEmpty(report.LogText)) LogMessage(report.LogText);
                    }),
                    RunProcessAsync
                );
            }
            catch (Exception ex)
            {
                LogMessage($"Fatal Processing Exception: {ex.Message}");
                StatusMessage = "Update process encountered a fatal error.";
            }
            finally
            {
                IsUpdating = false;
                UpdateProgress = 0;
                StatusMessage = "Ready.";
                vm.LastUpdate = DateTime.Now;
                _vmService!.SaveVirtualMachineEntry(vm.Model);
                if (!forceUpdate) vm.CalculateNextScheduledUpdate();
            }
        }
        private Task<int> RunProcessAsync(string fileName, string arguments)
        {
            var tcs = new TaskCompletionSource<int>();
            Trace.WriteLine($"RunProcessAsync({fileName}, {arguments})");
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(fileName),
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            process.Exited += (s, e) =>
            {
                string err = process.StandardError.ReadToEnd();
                string outText = process.StandardOutput.ReadToEnd(); // Grab stdout as well

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (!string.IsNullOrWhiteSpace(outText))
                        LogMessage($"[Console Output]: {outText.Trim()}");

                    if (!string.IsNullOrWhiteSpace(err))
                        LogMessage($"[Process StandardError]: {err.Trim()}");
                });

                tcs.SetResult(process.ExitCode);
                process.Dispose();
            };

            try { process.Start(); }
            catch (Exception ex) { LogMessage($"Process failed start parameters initialization: {ex.Message}"); tcs.SetResult(-1); }

            return tcs.Task;
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
        private void BrowseForVMWareExecutable()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executable Files (*.exe)|*.exe",
                Title = "Select vmrun.exe",
                InitialDirectory = "C:\\Program Files (x86)\\VMware\\VMware Workstation"
            };

            if (dialog.ShowDialog() == true)
            {
                Properties.Settings.Default.VMRunPath = dialog.FileName;
                Properties.Settings.Default.Save();
            }
        }
        private void BrowseForVirtualBoxExecutable()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executable Files (*.exe)|*.exe",
                Title = "Select VBoxManage.exe",
                InitialDirectory = "C:\\Program Files\\Oracle\\VirtualBox"
            };

            if (dialog.ShowDialog() == true)
            {
                Properties.Settings.Default.VBoxManagePath = dialog.FileName;
                Properties.Settings.Default.Save();
            }
        }
        private void BrowseForQEMUExecutable()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executable Files (*.exe)|*.exe",
                Title = "Select qemu.exe",
                InitialDirectory = "C:\\Program Files\\QEMU"
            };

            if (dialog.ShowDialog() == true)
            {
                Properties.Settings.Default.QEMUExecutablePath = dialog.FileName;
                Properties.Settings.Default.Save();
            }
        }
        public void InitializeApplicationProfiles()
        {
            List<VirtualMachineModel> models = VirtualMachineService.LoadVirtualMachineEntries();

            foreach (var model in models)
            {
                var vmViewModel = new VirtualMachineViewModel(model, _vmService, CollapseSiblings)
                {
                    DisplayName = !string.IsNullOrEmpty(model.VMPath) ? Path.GetFileNameWithoutExtension(model.VMPath) : "New Virtual Machine"
                };

                vmViewModel.RequestStartUpdate += async (vm, forceUpdate) => await ExecuteStartUpdate(vm, forceUpdate);
                vmViewModel.CalculateNextScheduledUpdate();
                VirtualMachines.Add(vmViewModel);
                Trace.WriteLine($"Loaded VM: {vmViewModel.DisplayName} from path: {model.VMPath} with hypervisor: {model.Hypervisor}");
            }
        }
    }
}