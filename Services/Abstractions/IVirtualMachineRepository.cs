using System.Collections.Generic;
using System.Threading.Tasks;
using VMUpdater.Models;

namespace VMUpdater.Services.Abstractions
{
    public interface IVirtualMachineRepository
    {
        Task SaveAsync(VirtualMachineModel vm);
        Task DeleteAsync(VirtualMachineModel vm);
        Task<IEnumerable<VirtualMachineModel>> LoadAllAsync();
    }
}