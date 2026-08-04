# <img src="VMUpdater.ico" alt="Icon" width="32"/> VMUpdater

A cross-platform desktop application that automates scheduled, headless system updates across multiple virtual machines.

<img src="Screenshots/MainWindow.png" alt="Main Window" />

## Overview

VMUpdater manages a list of VMs, each with its own hypervisor, guest OS, credentials, and weekly update schedule. At the scheduled time it headlessly boots the VM, verifies network connectivity from inside the guest, runs the appropriate package manager upgrade, then cleanly shuts the VM back down — all without any user interaction.

Hypervisors and guest OS types are fully user-configurable: define any hypervisor by providing its executable path and argument templates, and define any guest OS by providing its network check and update command templates.

## Features

- **Multi-VM Management** — Add, configure, and independently schedule as many VMs as you need.
- **Custom Hypervisor Support** — Define any hypervisor (e.g. VMware Workstation, VirtualBox, QEMU) by supplying its executable path and start/stop/run-script argument templates.
- **Custom Guest OS Support** — Define any guest OS by providing its network check and update command templates.
- **Scheduled Updates** — Per-VM weekly schedule (day + time), calculated and displayed as *Next Update*.
- **Sequential Update Queue** — Multiple update requests are processed one at a time; duplicate queuing is prevented.
- **Manual Trigger** — Force an immediate update for any VM at any time via *Update Now*.
- **Headless Execution** — VMs boot without a GUI and are shut down automatically after the update completes.
- **Network Validation** — Runs a configurable network check command inside the guest before updating; aborts cleanly on failure.
- **Progress Tracking** — A status bar and progress indicator keep you informed throughout each update.
- **Persistent Profiles** — VM, hypervisor, and guest OS configurations are each saved as JSON profiles in the app data directory.
- **System Tray Integration** — Minimizes to the system tray and shows live update status in the tooltip.
- **Activity Log** — In-app log tab plus timestamped log files provide a full audit trail.

## Requirements

- Windows 10/11, Linux, or macOS
- One or more hypervisors installed with a CLI interface (e.g. `vmrun`, `VBoxManage`, or a QEMU executable)
- Guest OS accessible over SSH with `sudo` privileges and a supported package manager

## Getting Started

1. **Clone the repository**
   ```bash
   git clone https://github.com/atshaw1994/VMUpdater.git
   ```

2. **Open in Visual Studio 2026** and build the solution.

3. **Add a hypervisor** — Open the Hypervisors tab, click *Add*, and provide the executable path and argument templates for start, stop, and run-script operations.

4. **Add a guest OS type** — Open the Guest OS tab, click *Add*, and provide the network check and update command templates.

5. **Add a VM** — Click *Add*, select your hypervisor and guest OS type, browse to the VM file, and enter credentials.

6. **Set a schedule** — Pick a day and time per VM. VMUpdater will handle the rest from the system tray.

## Project Structure

```
VMUpdater/
├── Models/
│   ├── VirtualMachineModel.cs              # Per-VM data model
│   ├── HypervisorModel.cs                  # Hypervisor definition (executable path & argument templates)
│   └── GuestOSModel.cs                     # Guest OS definition (network check & update command templates)
├── Services/
│   ├── Abstractions/
│   │   ├── IHypervisorUpdater.cs           # Update pipeline interface
│   │   ├── IHypervisorRepository.cs        # Hypervisor profile persistence abstraction
│   │   ├── IGuestOSRepository.cs           # Guest OS profile persistence abstraction
│   │   ├── IVirtualMachineRepository.cs    # VM profile persistence abstraction
│   │   └── IVirtualMachineService.cs       # Update orchestration abstraction
│   ├── Orchestration/
│   │   ├── GenericHypervisorUpdater.cs     # Data-driven update pipeline (start → check → update → stop)
│   │   ├── CommandTemplateExpander.cs      # Expands argument templates with runtime token values
│   │   ├── CommandTokens.cs                # Token definitions used in argument templates
│   │   ├── DefaultGuestOSTypes.cs          # Built-in guest OS type definitions
│   │   └── GuestOSProvider.cs              # Resolves guest OS configuration for a VM
│   ├── JsonHypervisorRepository.cs         # JSON-backed hypervisor profile persistence
│   ├── JsonGuestOSRepository.cs            # JSON-backed guest OS profile persistence
│   ├── JsonVirtualMachineRepository.cs     # JSON-backed VM profile persistence
│   └── VirtualMachineService.cs            # Orchestrates updates via the update pipeline
├── ViewModels/
│   ├── MainViewModel.cs                    # App-level logic, VM list, update queue
│   ├── VirtualMachineViewModel.cs          # Per-VM state, scheduling, commands
│   ├── AddHypervisorViewModel.cs           # Add/edit hypervisor dialog logic
│   ├── AddGuestOSViewModel.cs              # Add/edit guest OS dialog logic
│   └── AboutDialogViewModel.cs             # About dialog
├── Views/
│   ├── MainWindow.axaml                    # Main UI shell
│   ├── VirtualMachineEntry.axaml           # Expandable per-VM card
│   ├── AddNewHypervisorView.axaml          # Add/edit hypervisor dialog
│   ├── AddGuestOSView.axaml                # Add/edit guest OS dialog
│   ├── TimePicker.axaml                    # Custom time picker control
│   └── AboutDialog.axaml                   # About dialog

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
| Runtime | .NET 10 |
| UI Framework | [Avalonia UI](https://avaloniaui.net/) 12 + [FluentAvalonia](https://github.com/amwx/FluentAvalonia) |
| Architecture | MVVM + Service + Repository |
| Dependency Injection | [Microsoft.Extensions.DependencyInjection](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection) |
| MVVM Toolkit | [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) |
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