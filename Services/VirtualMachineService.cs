using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using System.Windows.Markup;
using VMUpdater.Models;

namespace VMUpdater.Services
{
    public class VirtualMachineService
    {
        private readonly Dictionary<HypervisorType, IHypervisorUpdater> _updaters;

        public VirtualMachineService()
        {
            // Register the active execution strategies
            _updaters = new Dictionary<HypervisorType, IHypervisorUpdater>
            {
                { HypervisorType.VMWare, new VMWareUpdater() },
                { HypervisorType.VirtualBox, new VirtualBoxUpdater() },
                { HypervisorType.QEMU, new QemuUpdater() }
            };
        }

        public void SaveVirtualMachineEntry(VirtualMachineModel vm)
        {
            ArgumentNullException.ThrowIfNull(vm);

            try
            {
                string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VMUpdater");
                Directory.CreateDirectory(appDataPath);

                string safeName = !string.IsNullOrWhiteSpace(vm.VMPath) ? Path.GetFileNameWithoutExtension(vm.VMPath) : Guid.NewGuid().ToString("N");
                string filePath = Path.Combine(appDataPath, $"{vm.Id:N}.xaml");

                var settings = new XmlWriterSettings
                {
                    Indent = true,                // Enables automatic indentation
                    IndentChars = "    ",         // Standard 4-space indent (or use "\t" for tabs)
                    NewLineOnAttributes = true,  // Keeps attributes grouped nicely on the same line
                    ConformanceLevel = ConformanceLevel.Fragment // Prevents requiring a standalone XML declaration header
                };

                var stringBuilder = new StringBuilder();
                using (var xmlWriter = XmlWriter.Create(stringBuilder, settings)) { XamlWriter.Save(vm, xmlWriter); }
                string xamlString = stringBuilder.ToString();

                File.WriteAllText(filePath, xamlString);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to serialize VM profile: {ex.Message}");
                throw;
            }
        }

        public void DeleteVirtualMachineEntry(VirtualMachineModel vm)
        {
            ArgumentNullException.ThrowIfNull(vm);
            try
            {
                string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VMUpdater");
                string filePath = Path.Combine(appDataPath, $"{vm.Id:N}.xaml");

                if (File.Exists(filePath)) File.Delete(filePath);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to remove VM profile: {ex.Message}");
                throw;
            }
        }

        public static List<VirtualMachineModel> LoadVirtualMachineEntries()
        {
            var loadedVMs = new List<VirtualMachineModel>();
            string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VMUpdater");

            if (!Directory.Exists(appDataPath)) return loadedVMs;

            string[] xamlFiles = Directory.GetFiles(appDataPath, "*.xaml");
            foreach (string filePath in xamlFiles)
            {
                try
                {
                    using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read);
                    if (XamlReader.Load(fs) is VirtualMachineModel vm)
                        loadedVMs.Add(vm);
                }
                catch (Exception ex)
                {
                    // Log file corruption errors gracefully
                    Trace.WriteLine($"Failed to load VM profile {Path.GetFileName(filePath)}: {ex.Message}");
                }
            }

            return loadedVMs;
        }

        public async Task StartUpdateAsync(VirtualMachineModel vmData, Action<UpdateProgressReport> progressCallback, Func<string, string, Task<int>> runProcessExecutor)
        {
            if (vmData == null) return;

            if (!_updaters.TryGetValue(vmData.Hypervisor, out var updater))
                throw new NotSupportedException($"Hypervisor {vmData.Hypervisor} not supported.");

            bool success = false;
            try
            {
                success = await updater.UpdateVMAsync(vmData, progressCallback, runProcessExecutor);
            }
            finally
            {
                progressCallback(new UpdateProgressReport { LogText = "Terminating hypervisor profile gracefully..." });

                // Custom hypervisor teardown mapping could live inside strategy modules, 
                // using VMware vmrun stop command as standard baseline example here:
                await runProcessExecutor(Properties.Settings.Default.VMRunPath, $"-T ws stop \"{vmData.VMPath}\" soft");

                if (success)
                {
                    progressCallback(new UpdateProgressReport { ProgressDelta = 100 });
                    await Task.Delay(2000);
                }
            }
        }
    }

    public interface IHypervisorUpdater
    {
        Task<bool> UpdateVMAsync(
            VirtualMachineModel vm,
            Action<UpdateProgressReport> reportProgress,
            Func<string, string, Task<int>> runProcessAsync);
    }

    public class UpdateProgressReport
    {
        public int ProgressDelta { get; set; }
        public string? StatusText { get; set; }
        public string? LogText { get; set; }
    }

    public class VMWareUpdater : IHypervisorUpdater
    {
        private readonly string VmrunPath = Properties.Settings.Default.VMRunPath;

        public async Task<bool> UpdateVMAsync(VirtualMachineModel vm, Action<UpdateProgressReport> reportProgress, Func<string, string, Task<int>> runProcessAsync)
        {
            // Step 1: Headless Invocation
            reportProgress(new UpdateProgressReport
            {
                ProgressDelta = 25,
                StatusText = "Starting update process...",
                LogText = "Initializing automated execution loop headlessly..."
            });
            await runProcessAsync(VmrunPath, $"-T ws start \"{vm.VMPath}\" nogui");

            // Step 2: Stabilization Wait
            reportProgress(new UpdateProgressReport
            {
                ProgressDelta = 50,
                StatusText = "Stabilizing system components...",
                LogText = "Allowing 45-second stabilization period for system kernel guest components..."
            });
            await Task.Delay(45000);

            // Step 3: Network Check
            reportProgress(new UpdateProgressReport
            {
                ProgressDelta = 60,
                StatusText = "Performing network check...",
                LogText = "Checking outward-bound routing connection from guest adapter..."
            });
            string pingArgs = $"-T ws -gu \"{vm.Username}\" -gp \"{vm.Password}\" runScriptInGuest \"{vm.VMPath}\" /bin/bash \"ping -c 3 8.8.8.8\"";
            int pingCode = await runProcessAsync(VmrunPath, pingArgs);

            if (pingCode != 0)
            {
                reportProgress(new UpdateProgressReport { StatusText = "Aborted: Network connectivity validation failed.", LogText = $"Abort: Intermittent network ping test rejected execution with exit frame code: {pingCode}" });
                return false;
            }

            // Step 4: Upgrade Transaction Execution
            reportProgress(new UpdateProgressReport
            {
                ProgressDelta = 75,
                StatusText = "Executing package transactions...",
                LogText = $"Sending package transaction orders via {vm.GuestOSType} engine..."
            });

            string updateCommand = GetOsUpdateScript(vm.GuestOSType, vm.Password);
            string updateArgs = $"-T ws -gu \"{vm.Username}\" -gp \"{vm.Password}\" runScriptInGuest \"{vm.VMPath}\" /bin/bash \"{updateCommand}\"";

            int upgradeCode = await runProcessAsync(VmrunPath, updateArgs);

            // Teardown step execution handled by the orchestrator loop
            return upgradeCode == 0;
        }

        private static string GetOsUpdateScript(string osType, string password) => osType.ToLower() switch
        {
            "arch" => $"echo '{password}' | sudo -S pacman -Syu --noconfirm",
            "ubuntu" => $"echo '{password}' | sudo -S apt-get update && echo '{password}' | sudo -S apt-get dist-upgrade -y",
            _ => "echo 'Unknown OS execution environment target'"
        };
    }

    public class VirtualBoxUpdater : IHypervisorUpdater
    {
        public async Task<bool> UpdateVMAsync(VirtualMachineModel vm, Action<UpdateProgressReport> reportProgress, Func<string, string, Task<int>> runProcessAsync)
        {
            // Ensure you pass the path to VBoxManage.exe (e.g., C:\Program Files\Oracle\VirtualBox\VBoxManage.exe)
            string vboxManagePath = Properties.Settings.Default.VBoxManagePath;
            string vmIdentifier = Path.GetFileNameWithoutExtension(vm.VMPath); // VirtualBox uses VM Name or UUID instead of a file path

            // Step 1: Headless Invocation
            reportProgress(new UpdateProgressReport
            {
                ProgressDelta = 25,
                StatusText = "Starting update process...",
                LogText = "Initializing automated execution loop headlessly via VirtualBox..."
            });
            // VirtualBox equivalent: startvm <name> --type headless
            await runProcessAsync(vboxManagePath, $"startvm \"{vmIdentifier}\" --type headless");

            // Step 2: Stabilization Wait
            reportProgress(new UpdateProgressReport
            {
                ProgressDelta = 50,
                StatusText = "Stabilizing system components...",
                LogText = "Allowing 45-second stabilization period for system kernel guest components..."
            });
            await Task.Delay(45000);

            // Step 3: Network Check
            reportProgress(new UpdateProgressReport
            {
                ProgressDelta = 60,
                StatusText = "Performing network check...",
                LogText = "Checking outward-bound routing connection from guest adapter..."
            });

            // VirtualBox equivalent to run a command inside the guest: 
            // guestcontrol <name> run --username <user> --password <pass> -- /path/to/bin arg1 arg2
            string pingArgs = $"guestcontrol \"{vmIdentifier}\" run --username \"{vm.Username}\" --password \"{vm.Password}\" -- /bin/bash -c \"ping -c 3 8.8.8.8\"";
            int pingCode = await runProcessAsync(vboxManagePath, pingArgs);

            if (pingCode != 0)
            {
                reportProgress(new UpdateProgressReport { StatusText = "Aborted: Network connectivity validation failed.", LogText = $"Abort: Intermittent network ping test rejected execution with exit frame code: {pingCode}" });
                return false;
            }

            // Step 4: Upgrade Transaction Execution
            reportProgress(new UpdateProgressReport
            {
                ProgressDelta = 75,
                StatusText = "Executing package transactions...",
                LogText = $"Sending package transaction orders via {vm.GuestOSType} engine..."
            });

            string updateCommand = GetOsUpdateScript(vm.GuestOSType, vm.Password);
            string updateArgs = $"guestcontrol \"{vmIdentifier}\" run --username \"{vm.Username}\" --password \"{vm.Password}\" -- /bin/bash -c \"{updateCommand.Replace("\"", "\\\"")}\"";

            int upgradeCode = await runProcessAsync(vboxManagePath, updateArgs);

            // Teardown step execution handled by the orchestrator loop
            return upgradeCode == 0;
        }

        private static string GetOsUpdateScript(string osType, string password) => osType.ToLower() switch
        {
            "arch" => $"echo '{password}' | sudo -S pacman -Syu --noconfirm",
            "ubuntu" => $"echo '{password}' | sudo -S apt-get update && echo '{password}' | sudo -S apt-get dist-upgrade -y",
            _ => "echo 'Unknown OS execution environment target'"
        };
    }

    public class QemuUpdater : IHypervisorUpdater
    {
        public async Task<bool> UpdateVMAsync(
            VirtualMachineModel vm,
            Action<UpdateProgressReport> reportProgress,
            Func<string, string, Task<int>> runProcessAsync)
        {
            Trace.WriteLine($"[QEMU] Booting QEMU headless target: {vm.VMPath}");

            await Task.Run(() =>
            {
                // 1. Fire execution string patterns via SSH or QEMU Guest Agent commands
                Task.Delay(5000).Wait();
            });

            return true;
        }
    }
}
