using System.Collections.Concurrent;
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
        private readonly ConcurrentQueue<(VirtualMachineViewModel VM, bool ForceUpdate)> _updateQueue = new();
        public Action<string>? OnTooltipRefreshRequested { get; set; }
        public ObservableCollection<VirtualMachineViewModel> VirtualMachines { get; }

        // Constructor for production
        public MainViewModel() : this(new VirtualMachineService()) { }

        // Constructor for testing (Dependency Injection)
        public MainViewModel(VirtualMachineService vmService)
        {
            _vmService = vmService; 
            VirtualMachines = []; 

            string logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs"); 
            if (!Directory.Exists(logFolder)) 
                Directory.CreateDirectory(logFolder); 
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"); 
            _logFilePath = Path.Combine(logFolder, $"{timestamp}.log"); 

            ShowMainWindowCommand =                 new RelayCommand(execute: ExecuteShowMainWindow); 
            ShowLogCommand =                        new RelayCommand(execute: ExecuteShowLog);
            UpdateAllCommand =                      new RelayCommand(execute: async () => ExecuteUpdateAll(), 
                                                                     canExecute: () => !IsUpdating && VirtualMachines?.Any() == true);
            BrowseForVMWareExecutableCommand =      new RelayCommand(execute: BrowseForVMWareExecutable); 
            BrowseForVirtualBoxExecutableCommand =  new RelayCommand(execute: BrowseForVirtualBoxExecutable); 
            BrowseForQEMUExecutableCommand =        new RelayCommand(execute: BrowseForQEMUExecutable); 
            ExitCommand =                           new RelayCommand(execute: () => Application.Current.Shutdown());
            AddVirtualMachineCommand =              new RelayCommand<string>(execute: AddVirtualMachine);
            RemoveVirtualMachineCommand =           new RelayCommand<VirtualMachineViewModel>(execute: RemoveVirtualMachine);

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
                    UpdateAllCommand.RaiseCanExecuteChanged();
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
        public RelayCommand ShowMainWindowCommand { get; }
        public RelayCommand ShowLogCommand { get; }
        public RelayCommand UpdateAllCommand { get; }
        public RelayCommand<string> AddVirtualMachineCommand { get; }
        public RelayCommand<VirtualMachineViewModel> RemoveVirtualMachineCommand { get; }
        public RelayCommand BrowseForVMWareExecutableCommand { get; }
        public RelayCommand BrowseForVirtualBoxExecutableCommand { get; }
        public RelayCommand BrowseForQEMUExecutableCommand { get; }
        public RelayCommand ExitCommand { get; }
        #endregion

        /// <summary>
        /// Shows the main window of the application, restoring it from a minimized state if necessary and bringing it to the foreground.
        /// </summary>
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

        /// <summary>
        /// Shows the main window and switches to the log tab, bringing it to the foreground.
        /// </summary>
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

        /// <summary>
        /// Adds a new virtual machine to the collection based on the specified hypervisor type.
        /// </summary>
        /// <param name="hypervisorType"></param>
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

            UpdateAllCommand.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// Removes a VM from the collection and deletes its entry from the service layer.
        /// </summary>
        /// <param name="itemToRemove"></param>
        private void RemoveVirtualMachine(VirtualMachineViewModel? itemToRemove)
        {
            if (itemToRemove != null)
            {
                VirtualMachines.Remove(itemToRemove);
                _vmService.DeleteVirtualMachineEntry(itemToRemove.Model);
            }

            UpdateAllCommand.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// Collapses all other VMs in the collection except for the one that was expanded.
        /// </summary>
        /// <param name="expandedItem"></param>
        private void CollapseSiblings(VirtualMachineViewModel expandedItem)
        {
            foreach (var vm in VirtualMachines.Where(vm => vm != expandedItem))
                vm.IsExpanded = false;
        }

        /// <summary>
        /// Executes an update for all VMs in the collection, queuing them one by one.
        /// </summary>
        private void ExecuteUpdateAll()
        {
            LogMessage("User initiated update for all VMs.");
            foreach (var vm in VirtualMachines)
            {
                EnqueueUpdateRequest(vm, forceUpdate: true);
            }
        }

        /// <summary>
        /// Entry point for VMs or Timers requesting an update.
        /// </summary>
        public void EnqueueUpdateRequest(VirtualMachineViewModel vm, bool forceUpdate = false)
        {
            // Avoid duplicate queue entries for the same VM if it's already queued
            if (_updateQueue.Any(item => item.VM == vm))
            {
                LogMessage($"[{vm.DisplayName}] Update request ignored: VM is already queued.");
                return;
            }

            _updateQueue.Enqueue((vm, forceUpdate));
            if (!IsUpdating)
                _ = ProcessNextInQueueAsync();
            else
                LogMessage($"[{vm.DisplayName}] Update request queued. Position in queue: {_updateQueue.Count}");
        }

        /// <summary>
        /// Processes the next VM in the update queue if the system is not currently updating.
        /// </summary>
        /// <returns></returns>
        private async Task ProcessNextInQueueAsync()
        {
            if (IsUpdating) return;

            if (_updateQueue.TryDequeue(out var request))
            {
                LogMessage($"Updating VM '{request.VM.DisplayName}'...");
                await ExecuteStartUpdate(request.VM, request.ForceUpdate);
            }
        }

        /// <summary>
        /// Executes the update process for a specific VM, handling progress reporting and logging.
        /// </summary>
        /// <param name="vm"></param>
        /// <param name="forceUpdate"></param>
        /// <returns></returns>
        public async Task ExecuteStartUpdate(VirtualMachineViewModel vm, bool forceUpdate = false)
        {
            if (IsUpdating) return;
            if (forceUpdate) LogMessage($"[{vm.DisplayName}] User started manual update.");

            IsUpdating = true;
            UpdateProgress = 10;
            StatusMessage = "Starting...";

            try
            {
                // Invoke service layer passing functional callback hooks 
                await _vmService.StartUpdateAsync(
                    vm.Model,
                    report => InvokeOnUIThread(() =>
                    {
                        if (report.ProgressDelta > 0) 
                            if (!_updateQueue.IsEmpty) UpdateProgress = report.ProgressDelta / (_updateQueue.Count + 1);
                            else UpdateProgress = report.ProgressDelta;
                        if (!string.IsNullOrEmpty(report.StatusText)) StatusMessage = report.StatusText;
                        if (!string.IsNullOrEmpty(report.LogText)) LogMessage($"[{vm.DisplayName}] {report.LogText}");
                    }),
                    (vmIdentifier, fileName, arguments) => RunProcessAsync(vm.DisplayName, fileName, arguments)
                );
            }
            catch (Exception ex)
            {
                LogMessage($"[{vm.DisplayName}] Fatal Processing Exception: {ex.Message}");
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

                // Drain the next queued VM automatically!
                _ = ProcessNextInQueueAsync(); 
            }
        }

        /// <summary>
        /// Runs an external process asynchronously and captures its output and error streams.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        private Task<int> RunProcessAsync(string vmIdentifier, string fileName, string arguments)
        {
            var tcs = new TaskCompletionSource<int>();

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = string.IsNullOrWhiteSpace(fileName) ? null : Path.GetDirectoryName(fileName),
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true
            };

            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            // Asynchronously log Standard Output as lines arrive
            process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        LogMessage($"[{vmIdentifier}] [StdOut]: {e.Data.Trim()}");
                    });
                }
            };

            // Asynchronously log Standard Error as lines arrive
            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        LogMessage($"[{vmIdentifier}] [StdErr]: {e.Data.Trim()}");
                    });
                }
            };

            process.Exited += (s, e) =>
            {
                // Set the task result and clean up process resources
                tcs.TrySetResult(process.ExitCode);
                process.Dispose();
            };

            try
            {
                process.Start();

                // CRITICAL: Begin asynchronous reading on both streams
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                LogMessage($"Process failed start parameters initialization: {ex.Message}");
                tcs.TrySetResult(-1);
                process.Dispose();
            }

            return tcs.Task;
        }

        /// <summary>
        /// Logs a message to both the log file and the bound LogText property for UI display.
        /// </summary>
        /// <param name="message"></param>
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

        /// <summary>
        /// Opens a file dialog for the user to select the VMWare executable (vmrun.exe) and saves the selected path to application settings.
        /// </summary>
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

        /// <summary>
        /// Opens a file dialog for the user to select the VirtualBox executable (VBoxManage.exe) and saves the selected path to application settings.
        /// </summary>
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

        /// <summary>
        /// Opens a file dialog for the user to select the QEMU executable (qemu.exe) and saves the selected path to application settings.
        /// </summary>
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

        /// <summary>
        /// Initializes the application by loading saved virtual machine entries from the service layer and creating corresponding view models for each entry.
        /// </summary>
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
            }
        }
    }
}