using VMUpdater.Models;
using VMUpdater.Services.Abstractions;

namespace VMUpdater.Services.Hypervisors
{
    public abstract class HypervisorUpdaterBase : IHypervisorUpdater
    {
        /// <summary>
        /// Specifies the hypervisor type handled by the implementing class.
        /// </summary>
        public abstract HypervisorModel Hypervisor { get; }

        /// <summary>
        /// Updates the specified virtual machine asynchronously, reporting progress and executing commands as needed.
        /// </summary>
        /// <param name="vm">The virtual machine to update.</param>
        /// <param name="reportProgress">An action to report progress updates.</param>
        /// <param name="runProcessAsync">A function to run processes asynchronously.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating success or failure.</returns>
        public abstract Task<bool> UpdateVMAsync(
            VirtualMachineModel vm,
            Action<UpdateProgressReport> reportProgress,
            Func<string, string, string, Task<int>> runProcessAsync);
    }
}