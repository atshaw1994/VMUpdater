using VMUpdater.Models;
using VMUpdater.Services.Abstractions;

namespace VMUpdater.Services.Hypervisors
{
    public abstract class HypervisorUpdaterBase : IHypervisorUpdater
    {
        /// <summary>
        /// Specifies the hypervisor type handled by the implementing class.
        /// </summary>
        public abstract HypervisorModel Hypervisor { get; }

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

            // Escape single quotes safely for POSIX shell single-quoted strings: ' -> '\''
            string safePassword = (password ?? string.Empty).Replace("'", "'\\''");

            // Standard wrapper to feed sudo password via STDIN safely
            string Sudo(string command) =>
                $"printf '%s\\n' '{safePassword}' | sudo -S -p '' sh -c '{command}'";

            return osType.Trim().ToLowerInvariant() switch
            {
                // Debian / Ubuntu Family
                "ubuntu" or "ubuntu server" or "debian" or "debian linux" or "pop os" or "pop!_os" or 
                "mint" or "linux mint" or "kali" or "kali linux" or "elementary" or "raspbian" =>
                    Sudo("export DEBIAN_FRONTEND=noninteractive; apt-get update && apt-get dist-upgrade -y && apt-get autoremove -y"),

                // RHEL / YUM Family (Enterprise Linux 7 / CentOS / Amazon Linux 2)
                "centos" or "red hat" or "redhat" or "rhel" or "amazon linux" or "amazon linux 2" or "oracle linux" =>
                    Sudo("yum update -y"),

                // Modern Fedora / DNF Family (RHEL 8+, AlmaLinux, Rocky Linux)
                "fedora" or "rocky" or "rocky linux" or "alma" or "almalinux" =>
                    Sudo("dnf upgrade --refresh -y"),

                // Arch Family
                "arch" or "arch linux" or "manjaro" or "endeavouros" =>
                    Sudo("pacman -Syu --noconfirm"),

                // SUSE Family
                "suse" or "opensuse" or "opensuse leap" or "opensuse tumbleweed" or "sles" =>
                    Sudo("zypper refresh && zypper update -y"),

                // Lightweight / Container / Edge Linux
                "alpine" or "alpine linux" =>
                    Sudo("apk update && apk upgrade"),

                // Void / Gentoo / FreeBSD
                "void" or "void linux" =>
                    Sudo("xbps-install -Su y"),

                // FreeBSD
                "freebsd" =>
                    Sudo("freebsd-update fetch install || pkg upgrade -y"),

                // macOS
                "mac" or "macos" or "mac os" or "mac os x" or "osx" =>
                    Sudo("softwareupdate -ia --verbose"),

                // Windows (10, 11, Server 2016/2019/2022)
                "windows" or "win" or "windows 10" or "windows 11" or "windows server" =>
                    "powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command " +
                    "\"if (-not (Get-Module -ListAvailable PSWindowsUpdate)) { Install-PackageProvider -Name " +
                    "NuGet -MinimumVersion 2.8.5.201 -Force; Install-Module PSWindowsUpdate -Force }; " +
                    "Get-WindowsUpdate -AcceptAll -Install -IgnoreReboot\"",

                _ => "echo 'Unknown OS execution environment target' && exit 1"
            };
        }
    }
}