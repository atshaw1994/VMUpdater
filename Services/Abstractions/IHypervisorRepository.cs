using VMUpdater.Models;

namespace VMUpdater.Services.Abstractions
{
    public interface IHypervisorRepository
    {
        Task SaveAsync(HypervisorModel hypervisor);
        Task DeleteAsync(HypervisorModel hypervisor);
        Task<IEnumerable<HypervisorModel>> LoadAllAsync();
    }
}
