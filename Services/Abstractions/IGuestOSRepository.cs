using VMUpdater.Models;

namespace VMUpdater.Services.Abstractions
{
    public interface IGuestOSRepository
    {
        Task SaveAsync(GuestOSModel guestOS);
        Task DeleteAsync(GuestOSModel guestOS);
        Task<IEnumerable<GuestOSModel>> LoadAllAsync();
        Task<GuestOSModel?> GetByIdAsync(Guid guestOSId);
    }
}
