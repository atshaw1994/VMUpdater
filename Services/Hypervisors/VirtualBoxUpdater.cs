using System.Diagnostics;
using System.IO;
using VMUpdater.Models;
using VMUpdater.Services.Abstractions;

namespace VMUpdater.Services.Hypervisors
{
    public class VirtualBoxUpdater() : HypervisorUpdaterBase
    {
        public override HypervisorType Hypervisor => HypervisorType.VirtualBox;

        public override async Task<bool> UpdateVMAsync(VirtualMachineModel vm, Action<UpdateProgressReport> reportProgress, Func<string, string, string, Task<int>> runProcessAsync)
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
    }
}
