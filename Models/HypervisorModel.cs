namespace VMUpdater.Models
{
    public class HypervisorModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Hypervisor";

        public string ExecutablePath { get; set; } = string.Empty;
        public string StartVMArgumentTemplate { get; set; } = string.Empty;
        public string StopVMArgumentTemplate { get; set; } = string.Empty;
        public string RunScriptArgumentTemplate { get; set; } = string.Empty;

    }
}
