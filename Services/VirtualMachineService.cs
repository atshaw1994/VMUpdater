using VMUpdater.Models;
using VMUpdater.Services.Abstractions;

namespace VMUpdater.Services
{
    public class VirtualMachineService(IEnumerable<IHypervisorUpdater> updaters) : IVirtualMachineService
    {
        /// <summary>
        /// A dictionary mapping hypervisor types to their corresponding updaters.
        /// </summary>
        private readonly IDictionary<HypervisorType, IHypervisorUpdater> _updaters = updaters.ToDictionary(u => u.Hypervisor, u => u);

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
}