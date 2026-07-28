using VMUpdater.Models;
using VMUpdater.Services.Orchestration;

namespace VMUpdater.Services.Abstractions
{
    public interface IHypervisorUpdater
    {
        HypervisorModel Hypervisor { get; }

        Task<bool> UpdateVMAsync(
            VirtualMachineModel vm,
            Action<UpdateProgressReport> reportProgress,
            Func<string, string, string, Task<int>> runProcessAsync);
    }
}