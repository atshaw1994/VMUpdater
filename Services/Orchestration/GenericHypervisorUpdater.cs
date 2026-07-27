using System.IO;
using VMUpdater.Models;
using VMUpdater.Services.Abstractions;
using VMUpdater.Services.Orchestration.VMUpdater.Services.Orchestration;

namespace VMUpdater.Services.Orchestration
{
    public class GenericHypervisorUpdater
    {
        public async Task<bool> UpdateVMAsync(
            HypervisorModel hypervisor,
            GuestOSModel guestOS,
            VirtualMachineModel vm,
            string scriptCommand,
            Action<UpdateProgressReport> reportProgress,
            Func<string, string, string, Task<int>> runProcessAsync)
        {
            string vmIdentifier = Path.GetFileNameWithoutExtension(vm.VMPath);

            // 1. Boot VM
            reportProgress(new UpdateProgressReport
            {
                ProgressDelta = 20,
                StatusText = $"Starting VM via {hypervisor.Name}..."
            });

            string startArgs = CommandTemplateExpander.ExpandArguments(hypervisor.StartVMArgumentTemplate, vm);

            int startCode = await runProcessAsync(vmIdentifier, hypervisor.ExecutablePath, startArgs);
            if (startCode != 0) return false;

            // 2. Network / Readiness Check
            if (!string.IsNullOrWhiteSpace(guestOS.NetworkCheckArgumentTemplate))
            {
                reportProgress(new UpdateProgressReport
                {
                    ProgressDelta = 40,
                    StatusText = "Checking guest readiness..."
                });

                string netArgs = CommandTemplateExpander.ExpandArguments(hypervisor.RunScriptArgumentTemplate, vm, GuestOSProvider.GetNetworkCheckCommand(guestOS.Name));

                int netCode = await runProcessAsync(vmIdentifier, hypervisor.ExecutablePath, netArgs);
                if (netCode != 0) return false;
            }

            // 3. Execute Guest Script
            reportProgress(new UpdateProgressReport
            {
                ProgressDelta = 75,
                StatusText = "Running guest updates..."
            });

            string runScriptArgs = CommandTemplateExpander.ExpandArguments(hypervisor.RunScriptArgumentTemplate, vm, scriptCommand);

            int scriptCode = await runProcessAsync(vmIdentifier, hypervisor.ExecutablePath, runScriptArgs);

            // 4. Graceful Shutdown
            reportProgress(new UpdateProgressReport
            {
                ProgressDelta = 90,
                StatusText = "Stopping VM..."
            });

            string stopArgs = CommandTemplateExpander.ExpandArguments(hypervisor.StopVMArgumentTemplate, vm);

            await runProcessAsync(vmIdentifier, hypervisor.ExecutablePath, stopArgs);

            return scriptCode == 0;
        }
    }
}