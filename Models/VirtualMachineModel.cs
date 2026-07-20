using System;
using System.Collections.Generic;
using System.Text;

namespace VMUpdater.Models
{
    public enum HypervisorType
    {
        VMWare,
        VirtualBox,
        QEMU
    }
    public class VirtualMachineModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public HypervisorType Hypervisor { get; set; } = HypervisorType.VMWare;
        public string GuestOSType { get; set; } = "Windows";
        public string VMPath { get; set; } = string.Empty;
        public string Username { get; set; } = "username";
        public string Password { get; set; } = "password";
        public string ScheduleDay { get; set; } = "Monday";
        public DateTime ScheduleTime { get; set; } = DateTime.MinValue;
        public DateTime LastUpdate { get; set; } = DateTime.MinValue;
        public DateTime NextUpdate { get; set; } = DateTime.MinValue;

    }
}
