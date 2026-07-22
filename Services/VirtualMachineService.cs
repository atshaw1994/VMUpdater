using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Markup;
using System.Xml;
using System.Xml.Linq;
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

        public virtual async Task StartUpdateAsync(
            VirtualMachineModel vmData,
            Action<UpdateProgressReport> progressCallback,
            Func<string, string, string, Task<int>> runProcessExecutor)
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
                if (success)
                {
                    progressCallback(new UpdateProgressReport
                    {
                        ProgressDelta = 100,
                        StatusText = "Update completed successfully.",
                        LogText = "Task finished successfully."
                    });
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
            Func<string, string, string, Task<int>> runProcessAsync);
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

        public async Task<bool> UpdateVMAsync(VirtualMachineModel vm, Action<UpdateProgressReport> reportProgress, Func<string, string, string, Task<int>> runProcessAsync)
        {
            string vmIdentifier = Path.GetFileNameWithoutExtension(vm.VMPath);

            // Step 1: Headless Invocation
            reportProgress(new UpdateProgressReport
            {
                ProgressDelta = 25,
                StatusText = "Starting update process...",
                LogText = "Initializing automated execution loop headlessly..."
            });
            await runProcessAsync(vmIdentifier, VmrunPath, $"-T ws start \"{vm.VMPath}\" nogui");

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
            int pingCode = await runProcessAsync(vmIdentifier, VmrunPath, pingArgs);

            if (pingCode != 0)
            {
                reportProgress(new UpdateProgressReport { StatusText = "Aborted: Network connectivity validation failed.", LogText = $"[{vmIdentifier}] Abort: Intermittent network ping test rejected execution with exit frame code: {pingCode}" });
                return false;
            }

            // Step 4: Upgrade Transaction Execution
            reportProgress(new UpdateProgressReport
            {
                ProgressDelta = 75,
                StatusText = "Executing package transactions...",
                LogText = "Sending package transaction orders via {vm.GuestOSType} engine..."
            });

            string updateCommand = GetOsUpdateScript(vm.GuestOSType, vm.Password);
            string updateArgs = $"-T ws -gu \"{vm.Username}\" -gp \"{vm.Password}\" runScriptInGuest \"{vm.VMPath}\" /bin/bash \"{updateCommand}\"";

            int upgradeCode = await runProcessAsync(vmIdentifier, VmrunPath, updateArgs);

            await StopVMAsync(VmrunPath, vm.VMPath, runProcessAsync);
            return upgradeCode == 0;
        }

        private static async Task StopVMAsync(string vmrunPath, string vmPath, Func<string, string, string, Task<int>> runProcessAsync)
        {
            await runProcessAsync(Path.GetFileNameWithoutExtension(vmPath), vmrunPath, $"-T ws stop \"{vmPath}\" soft");
        }

        private static string GetOsUpdateScript(string osType, string password)
        {
            string safePassword = password.Replace("\\", "\\\\").Replace("'", "'\\''");

            return osType.ToLower() switch
            {
                "arch" or "arch linux" =>
                    $"echo '{password}' | sudo -S pacman -Syu --noconfirm",

                "ubuntu" or "debian linux" or "debian" =>
                    $"printf '%s\\n' '{safePassword}' | sudo -S -p '' sh -c 'export DEBIAN_FRONTEND=noninteractive; apt-get update && apt-get dist-upgrade -y && apt-get autoremove -y'",

                _ => "echo 'Unknown OS execution environment target'"
            };
        }
    }

    public class VirtualBoxUpdater : IHypervisorUpdater
    {
        public async Task<bool> UpdateVMAsync(VirtualMachineModel vm, Action<UpdateProgressReport> reportProgress, Func<string, string, string, Task<int>> runProcessAsync)
        {
            string vboxManagePath = Properties.Settings.Default.VBoxManagePath;
            string vmIdentifier = Path.GetFileNameWithoutExtension(vm.VMPath);

            // Step 1: Headless Invocation
            reportProgress(new UpdateProgressReport
            {
                ProgressDelta = 25,
                StatusText = "Starting update process...",
                LogText = "Initializing automated execution loop headlessly via VirtualBox..."
            });
            await runProcessAsync(vmIdentifier, vboxManagePath, $"startvm \"{vmIdentifier}\" --type headless");

            // Step 2: Stabilization Wait
            reportProgress(new UpdateProgressReport
            {
                ProgressDelta = 50,
                StatusText = "Waiting for Guest Additions...",
                LogText = $"Waiting for guest execution service to initialize..."
            });

            bool isGuestReady = false;
            string pingArgs = $"guestcontrol \"{vmIdentifier}\" run --username \"{vm.Username}\" --password \"{vm.Password}\" -- /bin/bash -c \"ping -c 3 8.8.8.8\"";

            // Step 3: Network Check
            reportProgress(new UpdateProgressReport
            {
                ProgressDelta = 60,
                StatusText = "Performing network check...",
                LogText = "Checking outward-bound routing connection from guest adapter..."
            });

            // Attempt to reach the guest service up to 10 times (polling every 5 seconds = 50 seconds max)
            for (int attempt = 1; attempt <= 10; attempt++)
            {
                await Task.Delay(5000);
                int pingCode = await runProcessAsync(vmIdentifier, vboxManagePath, pingArgs);

                if (pingCode == 0)
                {
                    isGuestReady = true;
                    break;
                }
            }

            if (!isGuestReady)
            {
                reportProgress(new UpdateProgressReport
                {
                    StatusText = "Aborted: Guest execution service did not start or network test failed.",
                    LogText = "Abort: Guest control service unavailable or network ping rejected."
                });
                await StopVMAsync(vboxManagePath, vmIdentifier, runProcessAsync);
                return false;
            }

            // Step 4: Upgrade Transaction Execution
            reportProgress(new UpdateProgressReport
            {
                ProgressDelta = 75,
                StatusText = "Executing package transactions...",
                LogText = $"Sending package transaction orders via {vm.GuestOSType} engine..."
            });

            string scriptCommand = GetOsUpdateScript(vm.GuestOSType, vm.Password);
            string updateArgs = $"guestcontrol \"{vmIdentifier}\" run --username \"{vm.Username}\" --password \"{vm.Password}\" -- /bin/sh -c \"{scriptCommand}\"";

            int upgradeCode = await runProcessAsync(vmIdentifier, vboxManagePath, updateArgs);

            // Step 5: Graceful Teardown
            reportProgress(new UpdateProgressReport
            {
                ProgressDelta = 90,
                StatusText = "Shutting down VM...",
                LogText = "Terminating hypervisor profile gracefully..."
            });

            // Issue ACPI shutdown from VirtualBox host side
            await StopVMAsync(vboxManagePath, vmIdentifier, runProcessAsync);

            return upgradeCode == 0;
        }

        private static async Task StopVMAsync(string vboxManagePath, string vmIdentifier, Func<string, string, string, Task<int>> runProcessAsync)
        {
            // Step 1: Send the ACPI Power Button command
            await runProcessAsync(vmIdentifier, vboxManagePath, $"controlvm \"{vmIdentifier}\" acpipowerbutton");

            // Step 2: Poll for up to 30 seconds until VirtualBox confirms the VM state is "powered off"
            int timeoutSeconds = 30;
            while (timeoutSeconds > 0)
            {
                await Task.Delay(2000);
                timeoutSeconds -= 2;

                // Use process execution to query VM state
                var psi = new ProcessStartInfo
                {
                    FileName = vboxManagePath,
                    Arguments = $"showvminfo \"{vmIdentifier}\" --machinereadable",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    string output = await proc.StandardOutput.ReadToEndAsync();
                    await proc.WaitForExitAsync();

                    if (output.Contains("VMState=\"poweroff\""))
                    {
                        // VM has fully stopped
                        return;
                    }
                }
            }

            // Force power off if ACPI shutdown timed out
            Trace.WriteLine($"[VirtualBox] ACPI shutdown timed out for {vmIdentifier}. Forcing poweroff...");
            await runProcessAsync(vmIdentifier, vboxManagePath, $"controlvm \"{vmIdentifier}\" poweroff");
        }

        private static string GetOsUpdateScript(string osType, string password)
        {
            string safePassword = password.Replace("\\", "\\\\").Replace("'", "'\\''");

            return osType.Trim().ToLower() switch
            {
                "arch" or "arch linux" =>
                    $"printf '%s\\n' '{safePassword}' | sudo -S -p '' pacman -Syu --noconfirm",

                "ubuntu" or "debian linux" or "debian" =>
                    $"printf '%s\\n' '{safePassword}' | sudo -S -p '' sh -c 'export DEBIAN_FRONTEND=noninteractive; apt-get update && apt-get dist-upgrade -y && apt-get autoremove -y'",

                _ => "echo 'Unknown OS execution environment target' && exit 1"
            };
        }
    }

    public class QemuUpdater : IHypervisorUpdater
    {
        public async Task<bool> UpdateVMAsync(
            VirtualMachineModel vm,
            Action<UpdateProgressReport> reportProgress,
            Func<string, string, string, Task<int>> runProcessAsync)
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
