using VMUpdater.Models;
using VMUpdater.Services.Orchestration;

namespace VMUpdater.Services.Abstractions
{
    public interface IVirtualMachineService
    {
        Task StartUpdateAsync(
            VirtualMachineModel vmData,
            Action<UpdateProgressReport> progressCallback,
            Func<string, string, string, Task<int>> runProcessExecutor);
    }
}