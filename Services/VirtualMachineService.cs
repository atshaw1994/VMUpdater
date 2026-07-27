using VMUpdater.Models;
using VMUpdater.Services.Abstractions;
using VMUpdater.Services.Orchestration;
using VMUpdater.Services.Orchestration.VMUpdater.Services.Orchestration;

namespace VMUpdater.Services
{
    public class VirtualMachineService(GenericHypervisorUpdater updater, IHypervisorRepository hypervisorRepository, IGuestOSRepository guestOSRepository) : IVirtualMachineService
    {

        /// <summary>
        /// Starts the update process for a given virtual machine.
        /// </summary>
        /// <param name="vmData">The virtual machine data.</param>
        /// <param name="progressCallback">The callback to report progress.</param>
        /// <param name="runProcessExecutor">The function to execute processes.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="NotSupportedException"></exception>
        public async Task StartUpdateAsync(
            VirtualMachineModel vmData,
            Action<UpdateProgressReport> progressCallback,
            Func<string, string, string, Task<int>> runProcessExecutor)
        {
            if (vmData == null) return;

            HypervisorModel? hypervisor = await hypervisorRepository.GetByIdAsync(vmData.HypervisorId);
            if (hypervisor == null) return;

            GuestOSModel? guestOS = await guestOSRepository.GetByIdAsync(vmData.GuestOSId);
            if (guestOS == null) return;

            string guestOSUpdateScript = GuestOSProvider.GetOsUpdateScript(guestOS.Name, vmData.Password);

            bool success = false;
            try
            {
                success = await updater.UpdateVMAsync(hypervisor, guestOS, vmData, guestOSUpdateScript, progressCallback, runProcessExecutor);
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
}