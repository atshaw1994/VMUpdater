namespace VMUpdater.Models
{
    public class HypervisorModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Hypervisor";
        public string ExecutablePath { get; set; } = string.Empty;
    }
}
