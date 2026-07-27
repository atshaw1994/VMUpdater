using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using VMUpdater.Models;
using VMUpdater.Services.Abstractions;
using VMUpdater.Services.Orchestration;
using VMUpdater.Views;

namespace VMUpdater.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly string _logFilePath;
        private readonly IVirtualMachineService _vmService;
        private readonly IVirtualMachineRepository _vmRepository;
        private readonly IHypervisorRepository _hypervisorRepository;
        private readonly IGuestOSRepository _guestOSRepository;
        private readonly ConcurrentQueue<(VirtualMachineViewModel VM, bool ForceUpdate)> _updateQueue = new();

        public Action<string>? OnTooltipRefreshRequested { get; set; }
        public ObservableCollection<VirtualMachineViewModel> VirtualMachines { get; }
        public ObservableCollection<HypervisorModel> Hypervisors { get; }
        public ObservableCollection<GuestOSModel> GuestOSTypes { get; }

        // Primary Dependency Injection Constructor
        public MainViewModel(IVirtualMachineService vmService, IVirtualMachineRepository repository, IHypervisorRepository hypervisorRepository, IGuestOSRepository guestOSRepository)
        {
            _vmService = vmService;
            _vmRepository = repository;
            _hypervisorRepository = hypervisorRepository;
            _guestOSRepository = guestOSRepository;
            VirtualMachines = [];
            Hypervisors = [];
            GuestOSTypes = [];

            BindingOperations.EnableCollectionSynchronization(VirtualMachines, new object());

            string logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            if (!Directory.Exists(logFolder))
                Directory.CreateDirectory(logFolder);

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            _logFilePath = Path.Combine(logFolder, $"{timestamp}.log");

            _ = InitializeAsync();

            LogMessage("Logging profile initialized.");
        }

        #region Properties
        [ObservableProperty]
        public partial bool IsLogVisible { get; set; } = false;

        [ObservableProperty]
        public partial bool IsFindRowVisible { get; set; } = false;

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
        private static void About()
        {
            var aboutWindow = new AboutDialog
            {
                Owner = Application.Current?.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            aboutWindow.ShowDialog();
        }

        [RelayCommand]
        private void Export()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Zip Files (*.zip)|*.zip",
                Title = "Export Configuration Package",
                FileName = "VMUpdaterPackage.zip"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                if (File.Exists(dialog.FileName))
                {
                    File.Delete(dialog.FileName);
                }

                using (var zipArchive = ZipFile.Open(dialog.FileName, ZipArchiveMode.Create))
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };

                    // 1. Export Hypervisors
                    var hypervisorsEntry = zipArchive.CreateEntry("hypervisors.json");
                    using (var writer = new StreamWriter(hypervisorsEntry.Open()))
                    {
                        var json = JsonSerializer.Serialize(Hypervisors.ToList(), options);
                        writer.Write(json);
                    }

                    // 2. Export Machines
                    var machinesEntry = zipArchive.CreateEntry("machines.json");
                    using (var writer = new StreamWriter(machinesEntry.Open()))
                    {
                        var machineModels = VirtualMachines.Select(vm => vm.Model).ToList();
                        var json = JsonSerializer.Serialize(machineModels, options);
                        writer.Write(json);
                    }
                }

                LogMessage($"Successfully exported package to {dialog.FileName}.");
            }
            catch (Exception ex)
            {
                LogMessage($"Error exporting package: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task Import()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Zip Files (*.zip)|*.zip",
                Title = "Import Configuration Package"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                List<VirtualMachineModel> importedMachines = [];
                List<HypervisorModel> importedHypervisors = [];

                using (var zipArchive = ZipFile.OpenRead(dialog.FileName))
                {
                    // 1. Read Hypervisors first
                    var hypervisorEntry = zipArchive.GetEntry("hypervisors.json");
                    if (hypervisorEntry != null)
                    {
                        using var reader = new StreamReader(hypervisorEntry.Open());
                        var json = reader.ReadToEnd();
                        importedHypervisors = JsonSerializer.Deserialize<List<HypervisorModel>>(json) ?? [];
                    }

                    // 2. Read Machines
                    var machineEntry = zipArchive.GetEntry("machines.json");
                    if (machineEntry != null)
                    {
                        using var reader = new StreamReader(machineEntry.Open());
                        var json = reader.ReadToEnd();
                        importedMachines = JsonSerializer.Deserialize<List<VirtualMachineModel>>(json) ?? [];
                    }
                }

                // Process Hypervisors: Ignore existing items by ID
                int newHypervisorsCount = 0;
                foreach (var hvModel in importedHypervisors)
                {
                    if (!Hypervisors.Any(h => h.Id == hvModel.Id))
                    {
                        Hypervisors.Add(hvModel);
                        newHypervisorsCount++;
                        await _hypervisorRepository.SaveAsync(hvModel);
                    }
                }

                // Process Virtual Machines: Ignore existing items by ID
                int newMachinesCount = 0;
                foreach (var vmModel in importedMachines)
                {
                    var hypervisor = await _hypervisorRepository.GetByIdAsync(vmModel.HypervisorId);
                    var guestOS = await _guestOSRepository.GetByIdAsync(vmModel.GuestOSId);

                    if (hypervisor == null) throw new Exception("Hypervisor not found");
                    if (guestOS == null) throw new Exception("Guest OS not found");

                    if (!VirtualMachines.Any(vm => vm.Model.Id == vmModel.Id))
                    {
                        var vmViewModel = new VirtualMachineViewModel(
                            vmModel,
                            _vmService,
                            _vmRepository,
                            _hypervisorRepository,
                            _guestOSRepository,
                            Hypervisors,
                            GuestOSTypes,
                            CollapseSiblings
                        );

                        vmViewModel.RequestStartUpdate += async (vm, forceUpdate) => await ExecuteStartUpdate(vm, forceUpdate);

                        VirtualMachines.Add(vmViewModel);
                        newMachinesCount++;
                        await _vmRepository.SaveAsync(vmModel);
                    }
                }

                LogMessage($"Import complete. Added {newMachinesCount} new machines and {newHypervisorsCount} new hypervisors from {dialog.FileName}.");
            }
            catch (Exception ex)
            {
                LogMessage($"Error importing package: {ex.Message}");
            }
        }

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
        private void ShowLog()
        {
            if (Application.Current?.MainWindow is MainWindow mainWindow)
            {
                mainWindow.Show();
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Activate();
                IsLogVisible = true;
            }
        }

        [RelayCommand]
        private async Task AddHypervisor()
        {
            var dialog = new AddNewHypervisorView { Owner = Application.Current?.MainWindow };

            if (dialog.ShowDialog() == true && dialog.DataContext is AddHypervisorViewModel vm)
            {
                HypervisorModel newHypervisor = vm.CreatedHypervisor;

                // Save to repository
                await _hypervisorRepository.SaveAsync(newHypervisor);
            }
        }

        [RelayCommand]
        private void AddVirtualMachine()
        {
            var newModel = new VirtualMachineModel
            {
                VMPath = string.Empty,
                Username = string.Empty,
                Password = string.Empty,
                ScheduleDay = "Monday",
                ScheduleTime = DateTime.Now
            };

            var newItemViewModel = new VirtualMachineViewModel(
                newModel, 
                _vmService, 
                _vmRepository, 
                _hypervisorRepository,
                _guestOSRepository,
                Hypervisors,
                GuestOSTypes,
                CollapseSiblings) 
            { 
                IsExpanded = true 
            };
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
                await _vmRepository.DeleteAsync(itemToRemove.Model);
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
        private void ToggleFindRow() => IsFindRowVisible = !IsFindRowVisible;

        [RelayCommand]
        private void ToggleLog() => IsLogVisible = !IsLogVisible;

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
                request.VM.IsUpdating = true;
                await ExecuteStartUpdate(request.VM, request.ForceUpdate);
                request.VM.IsUpdating = false;
            }
        }

        public async Task ExecuteStartUpdate(VirtualMachineViewModel vm, bool forceUpdate = false)
        {
            if (IsUpdating) return;
            if (forceUpdate) LogMessage($"[{vm.DisplayName}] User started manual update.");

            IsUpdating = true;
            UpdateProgress = 0;
            vm.UpdateProgress = UpdateProgress;
            StatusMessage = "Starting...";

            try
            {
                await _vmService.StartUpdateAsync(
                    vm.Model,
                    report => Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (report.ProgressDelta > 0)
                        {
                            UpdateProgress = !_updateQueue.IsEmpty
                                ? report.ProgressDelta / (_updateQueue.Count + 1)
                                : report.ProgressDelta;

                            vm.UpdateProgress = report.ProgressDelta;
                        }

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
                vm.UpdateProgress = UpdateProgress;
                StatusMessage = "Ready.";
                vm.LastUpdate = DateTime.Now;
                await _vmRepository.SaveAsync(vm.Model);
                if (!forceUpdate) vm.CalculateNextScheduledUpdate();

                _ = ProcessNextInQueueAsync();
            }
        }

        private Task<int> RunProcessAsync(string vmIdentifier, string fileName, string arguments)
        {
            Trace.WriteLine($"runProcessAsync({vmIdentifier}, {fileName}, {arguments})");
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

        private async Task InitializeAsync()
        {
            await InitializeHypervisorsProfilesAsync();
            await InitializeGuestOSTypesAsync();
            await InitializeApplicationProfilesAsync();
        }

        public async Task InitializeApplicationProfilesAsync()
        {
            var models = await _vmRepository.LoadAllAsync();

            foreach (var model in models)
            {
                var hypervisor = await _hypervisorRepository.GetByIdAsync(model.HypervisorId);
                var guestOS = await _guestOSRepository.GetByIdAsync(model.GuestOSId);
                hypervisor ??= new HypervisorModel();
                guestOS ??= DefaultGuestOSTypes.Windows;

                var vmViewModel = new VirtualMachineViewModel(
                    model, 
                    _vmService, 
                    _vmRepository, 
                    _hypervisorRepository, 
                    _guestOSRepository, 
                    Hypervisors, 
                    GuestOSTypes, 
                    CollapseSiblings)
                {
                    DisplayName = !string.IsNullOrEmpty(model.VMPath) ? Path.GetFileNameWithoutExtension(model.VMPath) : "New Virtual Machine",
                    HypervisorType = Hypervisors.FirstOrDefault(h => h.Id == hypervisor.Id) ?? hypervisor,
                    GuestOSType = GuestOSTypes.FirstOrDefault(os => os.Id == guestOS.Id) ?? guestOS
                };

                vmViewModel.RequestStartUpdate += async (vm, forceUpdate) => await ExecuteStartUpdate(vm, forceUpdate);
                vmViewModel.CalculateNextScheduledUpdate();

                // Ensure collection modifications run on the WPF UI Thread
                if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
                {
                    dispatcher.Invoke(() => VirtualMachines.Add(vmViewModel));
                }
                else
                {
                    VirtualMachines.Add(vmViewModel);
                }
            }

            // recheck updateall command availability after loading profiles
            UpdateAllCommand.NotifyCanExecuteChanged();
        }

        private async Task InitializeHypervisorsProfilesAsync()
        {
            var hypervisors = await _hypervisorRepository.LoadAllAsync();

            // Ensure collection updates are on the UI thread
            Application.Current?.Dispatcher.Invoke(() =>
            {
                Hypervisors.Clear();
                foreach (var hypervisor in hypervisors)
                    Hypervisors.Add(hypervisor);
            });
        }

        private async Task InitializeGuestOSTypesAsync()
        {
            var guestOSTypes = await _guestOSRepository.LoadAllAsync();

            // Ensure collection updates are on the UI thread
            Application.Current?.Dispatcher.Invoke(() =>
            {
                GuestOSTypes.Clear();
                foreach (var guestOS in guestOSTypes)
                    GuestOSTypes.Add(guestOS);
            });
        }
    }
}