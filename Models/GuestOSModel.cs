namespace VMUpdater.Models
{
    public class GuestOSModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Guest OS";
        public string NetworkCheckArgumentTemplate { get; set; } = string.Empty;
    }
}
