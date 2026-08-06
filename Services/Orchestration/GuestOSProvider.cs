namespace VMUpdater.Services.Orchestration
{
    public static class GuestOSProvider
    {
        public static string GetNetworkCheckCommand(string osType)
        {
            if (string.IsNullOrWhiteSpace(osType))
                return "ping -c 3 -w 5 8.8.8.8";

            return osType.Trim().ToLowerInvariant() switch
            {
                // Debian / Ubuntu Family
                "ubuntu" or "ubuntu server" or "debian" or "debian linux" or "pop os" or "pop!_os" or
                "mint" or "linux mint" or "kali" or "kali linux" or "elementary" or "raspbian" =>
                    "ping -c 3 -w 5 8.8.8.8",

                // RHEL / YUM Family
                "centos" or "red hat" or "redhat" or "rhel" or "amazon linux" or "amazon linux 2" or "oracle linux" =>
                    "ping -c 3 -w 5 8.8.8.8",

                // Modern Fedora / DNF Family
                "fedora" or "rocky" or "rocky linux" or "alma" or "almalinux" =>
                    "ping -c 3 -w 5 8.8.8.8",

                // Arch Family
                "arch" or "arch linux" or "manjaro" or "endeavouros" =>
                    "ping -c 3 -w 5 8.8.8.8",

                // SUSE Family
                "suse" or "opensuse" or "opensuse leap" or "opensuse tumbleweed" or "sles" =>
                    "ping -c 3 -w 5 8.8.8.8",

                // Lightweight / Container / Edge Linux
                "alpine" or "alpine linux" =>
                    "ping -c 3 -w 5 8.8.8.8",

                // Void Linux
                "void" or "void linux" =>
                    "ping -c 3 -w 5 8.8.8.8",

                // FreeBSD
                "freebsd" =>
                    "ping -c 3 -t 5 8.8.8.8", // FreeBSD ping uses -t for total timeout instead of -w

                // macOS
                "mac" or "macos" or "mac os" or "mac os x" or "osx" =>
                    "ping -c 3 -t 5 8.8.8.8", // BSD-derived ping uses -t for total timeout

                // Windows (10, 11, Server 2016/2019/2022)
                "windows" or "win" or "windows 10" or "windows 11" or "windows server" =>
                    "ping -n 3 -w 2000 8.8.8.8",

                _ => "ping -c 3 -w 5 8.8.8.8"
            };
        }

        public static string GetOsUpdateScript(string osType, string password)
        {
            if (string.IsNullOrWhiteSpace(osType))
                return "echo 'Unknown OS execution environment target' && exit 1";

            // Escape single quotes safely for POSIX shell single-quoted strings: ' -> '\''
            string safePassword = (password ?? string.Empty).Replace("'", "'\\''");

            // Standard wrapper to feed sudo password via STDIN safely
            string Sudo(string command) => $"printf '%s\\n' '{safePassword}' | sudo -S -p '' sh -c '{command}'";

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
