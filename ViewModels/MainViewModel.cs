using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using VMUpdater.Models;
using VMUpdater.Services.Abstractions;
using VMUpdater.Views;

namespace VMUpdater.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly string _logFilePath;
        private readonly IVirtualMachineService _vmService;
        private readonly IVirtualMachineRepository _repository;
        private readonly ConcurrentQueue<(VirtualMachineViewModel VM, bool ForceUpdate)> _updateQueue = new();

        public Action<string>? OnTooltipRefreshRequested { get; set; }
        public ObservableCollection<VirtualMachineViewModel> VirtualMachines { get; }

        // Primary Dependency Injection Constructor
        public MainViewModel(IVirtualMachineService vmService, IVirtualMachineRepository repository)
        {
            _vmService = vmService;
            _repository = repository;
            VirtualMachines = [];

            string logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            if (!Directory.Exists(logFolder))
                Directory.CreateDirectory(logFolder);

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            _logFilePath = Path.Combine(logFolder, $"{timestamp}.log");

            _ = InitializeApplicationProfilesAsync();
            LogMessage("Logging profile initialized.");
        }

        #region Properties

        [ObservableProperty]
        public partial double UpdateProgress { get; set; } = 0.0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TrayToolTipText))]
        [NotifyCanExecuteChangedFor(nameof(UpdateAllCommand))]
        public partial bool IsUpdating { get; set; } = false;

        partial void OnIsUpdatingChanged(bool value) => OnTooltipRefreshRequested?.Invoke(TrayToolTipText);

        [ObservableProperty]
        public partial string LogText { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TrayToolTipText))]
        public partial string StatusMessage { get; set; } = "Ready.";

        public string TrayToolTipText => $"VMUpdater\n{(IsUpdating ? "Updating..." : "All VMs Updated.")}";

        public string VMWareExecutablePath
        {
            get => Properties.Settings.Default.VMRunPath;
            set => SetProperty(
                Properties.Settings.Default.VMRunPath,
                value,
                Properties.Settings.Default,
                (settings, val) => { settings.VMRunPath = val; settings.Save(); }
            );
        }

        public string VirtualBoxExecutablePath
        {
            get => Properties.Settings.Default.VBoxManagePath;
            set => SetProperty(
                Properties.Settings.Default.VBoxManagePath,
                value,
                Properties.Settings.Default,
                (settings, val) => { settings.VBoxManagePath = val; settings.Save(); }
            );
        }

        public string QEMUExecutablePath
        {
            get => Properties.Settings.Default.QEMUExecutablePath;
            set => SetProperty(
                Properties.Settings.Default.QEMUExecutablePath,
                value,
                Properties.Settings.Default,
                (settings, val) => { settings.QEMUExecutablePath = val; settings.Save(); }
            );
        }

        #endregion

        #region Commands

        [RelayCommand]
        private static void Exit() => Application.Current?.Shutdown();

        [RelayCommand]
        private static void ShowMainWindow()
        {
            if (Application.Current?.MainWindow is { } mainWindow)
            {
                mainWindow.Show();
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Activate();
            }
        }

        [RelayCommand]
        private static void ShowLog()
        {
            if (Application.Current?.MainWindow is MainWindow mainWindow)
            {
                mainWindow.Show();
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.MainTabControl.SelectedIndex = 1;
                mainWindow.Activate();
            }
        }

        [RelayCommand]
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

            var newItemViewModel = new VirtualMachineViewModel(newModel, _vmService, _repository, CollapseSiblings) { IsExpanded = true };
            newItemViewModel.RequestStartUpdate += async (vm, forceUpdate) => await ExecuteStartUpdate(vm, forceUpdate);
            VirtualMachines.Add(newItemViewModel);

            UpdateAllCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private async Task RemoveVirtualMachineAsync(VirtualMachineViewModel? itemToRemove)
        {
            if (itemToRemove != null)
            {
                VirtualMachines.Remove(itemToRemove);
                await _repository.DeleteAsync(itemToRemove.Model);
            }

            UpdateAllCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanUpdateAll))]
        private void UpdateAll()
        {
            LogMessage("User initiated update for all VMs.");
            foreach (var vm in VirtualMachines)
                EnqueueUpdateRequest(vm, forceUpdate: true);
        }
        private bool CanUpdateAll() => !IsUpdating && VirtualMachines?.Any() == true;

        [RelayCommand]
        private void BrowseForVMWareExecutable()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executable Files (*.exe)|*.exe",
                Title = "Select vmrun.exe",
                InitialDirectory = @"C:\Program Files (x86)\VMware\VMware Workstation"
            };

            if (dialog.ShowDialog() == true)
            {
                VMWareExecutablePath = dialog.FileName;
            }
        }

        [RelayCommand]
        private void BrowseForVirtualBoxExecutable()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executable Files (*.exe)|*.exe",
                Title = "Select VBoxManage.exe",
                InitialDirectory = @"C:\Program Files\Oracle\VirtualBox"
            };

            if (dialog.ShowDialog() == true)
            {
                VirtualBoxExecutablePath = dialog.FileName;
            }
        }

        [RelayCommand]
        private void BrowseForQEMUExecutable()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executable Files (*.exe)|*.exe",
                Title = "Select qemu.exe",
                InitialDirectory = @"C:\Program Files\QEMU"
            };

            if (dialog.ShowDialog() == true)
            {
                QEMUExecutablePath = dialog.FileName;
            }
        }

        #endregion

        private void CollapseSiblings(VirtualMachineViewModel expandedItem)
        {
            foreach (var vm in VirtualMachines.Where(vm => vm != expandedItem))
                vm.IsExpanded = false;
        }

        public void EnqueueUpdateRequest(VirtualMachineViewModel vm, bool forceUpdate = false)
        {
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

        private async Task ProcessNextInQueueAsync()
        {
            if (IsUpdating) return;

            if (_updateQueue.TryDequeue(out var request))
            {
                LogMessage($"Updating VM '{request.VM.DisplayName}'...");
                await ExecuteStartUpdate(request.VM, request.ForceUpdate);
            }
        }

        public async Task ExecuteStartUpdate(VirtualMachineViewModel vm, bool forceUpdate = false)
        {
            if (IsUpdating) return;
            if (forceUpdate) LogMessage($"[{vm.DisplayName}] User started manual update.");

            IsUpdating = true;
            UpdateProgress = 10;
            StatusMessage = "Starting...";

            try
            {
                await _vmService.StartUpdateAsync(
                    vm.Model,
                    report => Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (report.ProgressDelta > 0)
                            UpdateProgress = !_updateQueue.IsEmpty
                                ? report.ProgressDelta / (_updateQueue.Count + 1)
                                : report.ProgressDelta;

                        if (!string.IsNullOrEmpty(report.StatusText))
                            StatusMessage = report.StatusText;

                        if (!string.IsNullOrEmpty(report.LogText))
                            LogMessage($"[{vm.DisplayName}] {report.LogText}");
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
                await _repository.SaveAsync(vm.Model);
                if (!forceUpdate) vm.CalculateNextScheduledUpdate();

                _ = ProcessNextInQueueAsync();
            }
        }

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
                tcs.TrySetResult(process.ExitCode);
                process.Dispose();
            };

            try
            {
                process.Start();
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

        public void LogMessage(string message)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string formatLine = $"[{timestamp}] {message}{Environment.NewLine}";
                File.AppendAllText(_logFilePath, formatLine);

                LogText += formatLine;
            }
            catch { /* Prevent filesystem context lock crashes */ }
        }

        public async Task InitializeApplicationProfilesAsync()
        {
            var models = await _repository.LoadAllAsync();

            foreach (var model in models)
            {
                var vmViewModel = new VirtualMachineViewModel(model, _vmService, _repository, CollapseSiblings)
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