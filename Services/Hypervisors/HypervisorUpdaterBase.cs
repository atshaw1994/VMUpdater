using VMUpdater.Models;
using VMUpdater.Services.Abstractions;

namespace VMUpdater.Services.Hypervisors
{
    public abstract class HypervisorUpdaterBase : IHypervisorUpdater
    {
        /// <summary>
        /// Specifies the hypervisor type handled by the implementing class.
        /// </summary>
        public abstract HypervisorType Hypervisor { get; }

        /// <summary>
        /// Updates the specified virtual machine asynchronously, reporting progress and executing commands as needed.
        /// </summary>
        /// <param name="vm">The virtual machine to update.</param>
        /// <param name="reportProgress">An action to report progress updates.</param>
        /// <param name="runProcessAsync">A function to run processes asynchronously.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating success or failure.</returns>
        public abstract Task<bool> UpdateVMAsync(
            VirtualMachineModel vm,
            Action<UpdateProgressReport> reportProgress,
            Func<string, string, string, Task<int>> runProcessAsync);

        /// <summary>
        /// Generates the appropriate OS update command based on the specified OS type and password.
        /// </summary>
        /// <param name="osType">The type of the operating system.</param>
        /// <param name="password">The password for the OS.</param>
        /// <returns>The command to update the OS.</returns>
        protected virtual string GetOsUpdateScript(string osType, string password)
        {
            if (string.IsNullOrWhiteSpace(osType))
                return "echo 'Unknown OS execution environment target' && exit 1";

            string safePassword = password.Replace("\\", "\\\\").Replace("'", "'\\''");

            return osType.Trim().ToLowerInvariant() switch
            {
                "arch" or "arch linux" =>
                    $"printf '%s\\n' '{safePassword}' | sudo -S -p '' pacman -Syu --noconfirm",

                "ubuntu" or "debian linux" or "debian" =>
                    $"printf '%s\\n' '{safePassword}' | sudo -S -p '' sh -c 'export DEBIAN_FRONTEND=noninteractive; apt-get update && apt-get dist-upgrade -y && apt-get autoremove -y'",

                _ => "echo 'Unknown OS execution environment target' && exit 1"
            };
        }
    }
}