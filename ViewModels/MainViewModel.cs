// VMUpdater - Automated headless VM update scheduler
// Copyright (C) 2025 Aaron Shaw
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using VMUpdater.Models;
using VMUpdater.Services.Abstractions;
using VMUpdater.Services.Orchestration;
using VMUpdater.Views;

namespace VMUpdater.ViewModels
{
    public record MainServicesContext(
        IVirtualMachineService VmService,
        IVirtualMachineRepository VmRepository,
        IHypervisorRepository HypervisorRepository,
        IGuestOSRepository GuestOSRepository
    );

    public partial class MainViewModel : ObservableObject
    {
        private readonly MainServicesContext _services;
        private readonly string _logFilePath;
        private readonly ConcurrentQueue<(VirtualMachineViewModel VM, bool ForceUpdate)> _updateQueue = new();

        public ObservableCollection<VirtualMachineViewModel> VirtualMachines { get; } = [];
        public ObservableCollection<VirtualMachineViewModel> FilteredVirtualMachines { get; } = [];
        public ObservableCollection<HypervisorModel> Hypervisors { get; } = [];
        public ObservableCollection<GuestOSModel> GuestOSTypes { get; } = [];

        public MainViewModel() { }

        public MainViewModel(MainServicesContext services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));

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
        public partial string SearchText { get; set; } = string.Empty;
        partial void OnSearchTextChanged(string value) => RefreshFilteredMachines();

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

        [ObservableProperty]
        public partial string LogText { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TrayToolTipText))]
        public partial string StatusMessage { get; set; } = "Ready.";

        public string TrayToolTipText => $"VMUpdater\n{(IsUpdating ? "Updating..." : "All VMs Updated.")}";

        #endregion

        #region Commands

        [RelayCommand]
        private static async Task AboutAsync()
        {
            if ((Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow is { } mainWindow)
            {
                var aboutWindow = new AboutDialog();
                await aboutWindow.ShowDialog(mainWindow);
            }
        }

        [RelayCommand]
        private async Task ExportAsync()
        {
            if ((Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow is not { } mainWindow)
                return;

            var topLevel = TopLevel.GetTopLevel(mainWindow);
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Configuration Package",
                SuggestedFileName = "VMUpdaterPackage.zip",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Zip Files") { Patterns = ["*.zip"] }
                }
            });

            if (file == null) return;

            string filePath = file.Path.LocalPath;

            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                using (var zipArchive = ZipFile.Open(filePath, ZipArchiveMode.Create))
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

                LogMessage($"Successfully exported package to {filePath}.");
            }
            catch (Exception ex)
            {
                LogMessage($"Error exporting package: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ImportAsync()
        {
            if ((Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow is not { } mainWindow)
                return;

            var topLevel = TopLevel.GetTopLevel(mainWindow);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import Configuration Package",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Zip Files") { Patterns = new[] { "*.zip" } }
                }
            });

            var file = files.FirstOrDefault();
            if (file == null) return;

            string filePath = file.Path.LocalPath;

            try
            {
                List<VirtualMachineModel> importedMachines = [];
                List<HypervisorModel> importedHypervisors = [];

                using (var zipArchive = ZipFile.OpenRead(filePath))
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
                        await _services.HypervisorRepository.SaveAsync(hvModel);
                    }
                }

                // Process Virtual Machines: Ignore existing items by ID
                int newMachinesCount = 0;
                foreach (var vmModel in importedMachines)
                {
                    var hypervisor = await _services.HypervisorRepository.GetByIdAsync(vmModel.HypervisorId);
                    var guestOS = await _services.GuestOSRepository.GetByIdAsync(vmModel.GuestOSId);

                    if (hypervisor == null) throw new Exception("Hypervisor not found");
                    if (guestOS == null) throw new Exception("Guest OS not found");

                    if (!VirtualMachines.Any(vm => vm.Model.Id == vmModel.Id))
                    {
                        var vmViewModel = CreateVMViewModel(vmModel);
                        vmViewModel.RequestStartUpdate += async (vm, forceUpdate) => await ExecuteStartUpdate(vm, forceUpdate);
                        VirtualMachines.Add(vmViewModel);
                        newMachinesCount++;
                        await _services.VmRepository.SaveAsync(vmModel);
                    }
                }

                RefreshFilteredMachines();
                LogMessage($"Import complete. Added {newMachinesCount} new machines and {newHypervisorsCount} new hypervisors from {filePath}.");
            }
            catch (Exception ex)
            {
                LogMessage($"Error importing package: {ex.Message}");
            }
        }

        [RelayCommand]
        private static void Exit() => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();

        [RelayCommand]
        private static void ShowMainWindow()
        {
            if ((Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow is { } mainWindow)
            {
                mainWindow.Show();
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Activate();
            }
        }

        [RelayCommand]
        private void ShowLog()
        {
            if ((Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow is MainWindow mainWindow)
            {
                mainWindow.Show();
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Activate();
                IsLogVisible = true;
            }
        }

        [RelayCommand]
        private async Task AddHypervisorAsync()
        {
            if ((Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow is not { } mainWindow)
                return;

            var dialog = new AddNewHypervisorView();
            var result = await dialog.ShowDialog<bool>(mainWindow);

            if (result && dialog.DataContext is AddHypervisorViewModel vm)
            {
                HypervisorModel newHypervisor = vm.CreatedHypervisor;
                await _services.HypervisorRepository.SaveAsync(newHypervisor);
                Hypervisors.Add(newHypervisor);
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

            var newItemViewModel = CreateVMViewModel(newModel);
            newItemViewModel.IsExpanded = true;
            newItemViewModel.RequestStartUpdate += async (vm, forceUpdate) => await ExecuteStartUpdate(vm, forceUpdate);

            VirtualMachines.Add(newItemViewModel);
            RefreshFilteredMachines();

            UpdateAllCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private async Task AddGuestOSAsync()
        {
            if ((Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow is not { } mainWindow)
                return;

            var dialog = new AddGuestOSView();
            var result = await dialog.ShowDialog<bool>(mainWindow);

            if (result && dialog.DataContext is AddGuestOSViewModel vm)
            {
                GuestOSModel newGuestOS = vm.CreatedGuestOS;
                await _services.GuestOSRepository.SaveAsync(newGuestOS);
                GuestOSTypes.Add(newGuestOS);
            }
        }

        [RelayCommand]
        private async Task RemoveVirtualMachineAsync(VirtualMachineViewModel? itemToRemove)
        {
            if (itemToRemove != null)
            {
                VirtualMachines.Remove(itemToRemove);
                RefreshFilteredMachines();
                await _services.VmRepository.DeleteAsync(itemToRemove.Model);
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
        private bool CanUpdateAll() => !IsUpdating && VirtualMachines.Any();

        [RelayCommand]
        private void ToggleFindRow()
        {
            IsFindRowVisible = !IsFindRowVisible;
            if (!IsFindRowVisible)
                SearchText = string.Empty;
        }

        [RelayCommand]
        private void ToggleLog() => IsLogVisible = !IsLogVisible;

        #endregion

        private void RefreshFilteredMachines()
        {
            FilteredVirtualMachines.Clear();
            var query = VirtualMachines.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                query = query.Where(vm => vm.DisplayName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true);
            }

            foreach (var vm in query.OrderBy(vm => vm.DisplayName))
            {
                FilteredVirtualMachines.Add(vm);
            }
        }

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
                await _services.VmService.StartUpdateAsync(
                    vm.Model,
                    report => Dispatcher.UIThread.InvokeAsync(() =>
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
                await _services.VmRepository.SaveAsync(vm.Model);
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
                    _ = Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        LogMessage($"[{vmIdentifier}] [StdOut]: {e.Data.Trim()}");
                    });
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    Dispatcher.UIThread.InvokeAsync(() =>
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
            var models = await _services.VmRepository.LoadAllAsync();

            foreach (var model in models)
            {
                var hypervisor = await _services.HypervisorRepository.GetByIdAsync(model.HypervisorId);
                var guestOS = await _services.GuestOSRepository.GetByIdAsync(model.GuestOSId);
                hypervisor ??= new HypervisorModel();
                guestOS ??= DefaultGuestOSTypes.Windows;

                VirtualMachineViewModel vmViewModel = CreateVMViewModel(model);
                vmViewModel.DisplayName = !string.IsNullOrEmpty(model.VMPath) ? Path.GetFileNameWithoutExtension(model.VMPath) : "New Virtual Machine";
                vmViewModel.HypervisorType = Hypervisors.FirstOrDefault(h => h.Id == hypervisor.Id) ?? hypervisor;
                vmViewModel.GuestOSType = GuestOSTypes.FirstOrDefault(os => os.Id == guestOS.Id) ?? guestOS;
                vmViewModel.RequestStartUpdate += async (vm, forceUpdate) => await ExecuteStartUpdate(vm, forceUpdate);
                vmViewModel.CalculateNextScheduledUpdate();

                if (!Dispatcher.UIThread.CheckAccess())
                {
                    await Dispatcher.UIThread.InvokeAsync(() => VirtualMachines.Add(vmViewModel));
                }
                else
                {
                    VirtualMachines.Add(vmViewModel);
                }
            }

            RefreshFilteredMachines();
            UpdateAllCommand.NotifyCanExecuteChanged();
        }

        private async Task InitializeHypervisorsProfilesAsync()
        {
            var hypervisors = await _services.HypervisorRepository.LoadAllAsync();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Hypervisors.Clear();
                foreach (var hypervisor in hypervisors)
                    Hypervisors.Add(hypervisor);
            });
        }

        private async Task InitializeGuestOSTypesAsync()
        {
            var guestOSTypes = await _services.GuestOSRepository.LoadAllAsync();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                GuestOSTypes.Clear();
                foreach (var guestOS in guestOSTypes)
                    GuestOSTypes.Add(guestOS);
            });
        }

        private VirtualMachineViewModel CreateVMViewModel(VirtualMachineModel model)
        {
            var vmContext = new VMViewModelContext(
                _services.VmService,
                _services.VmRepository,
                _services.HypervisorRepository,
                _services.GuestOSRepository,
                Hypervisors,
                GuestOSTypes,
                CollapseSiblings
            );

            return new VirtualMachineViewModel(model, vmContext);
        }
    }
}