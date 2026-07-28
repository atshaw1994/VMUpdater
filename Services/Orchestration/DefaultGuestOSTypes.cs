using VMUpdater.Models;

namespace VMUpdater.Services.Orchestration
{
    public static class DefaultGuestOSTypes
    {
        public static readonly GuestOSModel Ubuntu = new()
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Ubuntu",
            NetworkCheckArgumentTemplate = "ping -c 3 -w 5 8.8.8.8"
        };

        public static readonly GuestOSModel Arch = new()
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Name = "Arch Linux",
            NetworkCheckArgumentTemplate = "ping -c 3 -w 5 8.8.8.8"
        };

        public static readonly GuestOSModel Windows = new()
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Name = "Windows",
            NetworkCheckArgumentTemplate = "ping -n 3 -w 2000 8.8.8.8"
        };

        public static bool IsDefaultGuestOS(Guid guid) => guid == Ubuntu.Id || guid == Arch.Id || guid == Windows.Id;

        public static GuestOSModel GetModelById(Guid guid)
        {
            if (guid == Ubuntu.Id) return Ubuntu;
            if (guid == Arch.Id) return Arch;
            if (guid == Windows.Id) return Windows;
            throw new ArgumentException("The provided GUID does not correspond to a default Guest OS type.", nameof(guid));
        }
    }
}
