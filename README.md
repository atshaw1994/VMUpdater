# <img src="VMUpdater.ico" alt="Icon" width="32"/> VMUpdater

This branch is for implementing the ability to enter custom hypervisors via locating their respective hypervisor binary tool. 

## Requirements

- Windows 10/11
- One or more of the following hypervisors installed:

  | Hypervisor | Executable |
  |---|---|
  | [VMware Workstation](https://www.vmware.com/products/workstation-pro.html) | `vmrun.exe` |
  | [VirtualBox](https://www.virtualbox.org/) | `VBoxManage.exe` |
  | [QEMU](https://www.qemu.org/) | QEMU executable |

- Guest OS with `sudo` privileges and a supported package manager (`pacman` or `apt-get`)

## Getting Started

1. **Clone the repository**
   ```bash
   git clone https://github.com/atshaw1994/VMUpdater.git
   ```

2. **Open in Visual Studio 2026** and build the solution.

3. **Set your hypervisor executable paths** in the Settings tab (VMRun, VBoxManage, QEMU).

4. **Add a VM** — Click *Add*, select your hypervisor and guest OS type, browse to the VM file, and enter credentials.

5. **Set a schedule** — Pick a day and time per VM. VMUpdater will handle the rest from the system tray.

## Project Structure

```
VMUpdater/
├── Models/
│   └── VirtualMachineModel.cs              # Per-VM data model & HypervisorType enum
├── Services/
│   ├── Abstractions/
│   │   ├── IHypervisorUpdater.cs           # Strategy interface for hypervisor implementations
│   │   ├── ISettingsProvider.cs            # Settings abstraction for testability
│   │   ├── IVirtualMachineRepository.cs    # Persistence abstraction
│   │   └── IVirtualMachineService.cs       # Update orchestration abstraction
│   ├── Hypervisors/
│   │   ├── HypervisorUpdaterBase.cs        # Shared update pipeline logic
│   │   ├── VMWareUpdater.cs
│   │   ├── VirtualBoxUpdater.cs
│   │   └── QemuUpdater.cs
│   ├── AppSettingsProvider.cs              # Reads hypervisor paths from user settings
│   ├── JsonVirtualMachineRepository.cs     # JSON-backed VM profile persistence
│   └── VirtualMachineService.cs            # Orchestrates updates via hypervisor strategy
├── ViewModels/
│   ├── MainViewModel.cs                    # App-level logic, VM list, update queue
│   └── VirtualMachineViewModel.cs          # Per-VM state, scheduling, browse
├── Views/
│   ├── MainWindow.xaml                     # Main UI shell
│   ├── VirtualMachineEntry.xaml            # Expandable per-VM card
│   └── TimePicker.xaml                     # Custom time picker control
├── Helpers/
│   ├── BooleanToVisibilityConverter.cs
│   ├── InverseBooleanConverter.cs
│   └── DateTimeToPartsConverter.cs
└── Properties/
    └── Settings.settings                   # Hypervisor executable paths

VMUpdater.Tests/
├── Services/
│   └── VirtualMachineServiceTests.cs       # Update pipeline & abort behaviour
└── ViewModels/
    ├── MainViewModelQueueTests.cs          # Update queue, DI, tray tooltip
    └── VirtualMachineViewModelTests.cs     # Scheduling, display text, commands
```

## Tech Stack

| | |
|---|---|
| Runtime | .NET 10 / WPF |
| Architecture | MVVM + Service/Strategy + Repository |
| Dependency Injection | [Microsoft.Extensions.DependencyInjection](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection) |
| MVVM Toolkit | [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) |
| System Tray | [H.NotifyIcon.Wpf](https://github.com/HavenDV/H.NotifyIcon) |
| Testing | [xUnit v3](https://xunit.net/) + [NSubstitute](https://nsubstitute.github.io/) |
| Coverage | [coverlet](https://github.com/coverlet-coverage/coverlet) |

## Running Tests

```bash
dotnet test VMUpdater.Tests/VMUpdater.Tests.csproj
```

To collect code coverage:

```bash
dotnet test VMUpdater.Tests/VMUpdater.Tests.csproj --collect:"XPlat Code Coverage"
```

## License

This project is licensed under the **GNU General Public License v3.0**. See [LICENSE](LICENSE) for the full license text.

You are free to use, modify, and distribute this software under the terms of the GPL v3. Any derivative works must also be distributed under the same license.
