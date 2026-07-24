using VMUpdater.Models;

namespace VMUpdater.Services.Abstractions
{
    public interface IHypervisorUpdater
    {
        HypervisorType Hypervisor { get; }

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
}