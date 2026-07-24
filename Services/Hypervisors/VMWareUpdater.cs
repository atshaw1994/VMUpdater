using System.IO;
using VMUpdater.Models;
using VMUpdater.Services.Abstractions;

namespace VMUpdater.Services.Hypervisors
{
    public class VMWareUpdater() : HypervisorUpdaterBase
    {
        private readonly string VmrunPath = Properties.Settings.Default.VMRunPath;
        public override HypervisorType Hypervisor => HypervisorType.VMWare;

        public override async Task<bool> UpdateVMAsync(VirtualMachineModel vm, Action<UpdateProgressReport> reportProgress, Func<string, string, string, Task<int>> runProcessAsync)
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
                LogText = $"Sending package transaction orders via {vm.GuestOSType} engine..."
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

    }
}
