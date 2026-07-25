namespace VMUpdater.Models
{
    public class HypervisorModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Hypervisor";
        public string ExecutablePath { get; set; } = string.Empty;
        public string StartVMArgument { get; set; } = string.Empty;
        public string NetworkCheckArgument { get; set; } = string.Empty;
        public string UpdateVMArgument { get; set; } = string.Empty;
        public string EndVMArgument { get; set; } = string.Empty;

    }
}
