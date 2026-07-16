# <img src="VMUpdater.ico" alt="Icon" width="32"/> VMUpdater

A lightweight Windows desktop application that automates scheduled system updates for VMware Workstation virtual machines running Arch Linux (or any `yay`/`pacman`-based distro) — all without any manual interaction.

<img src="Screenshots/MainWindow.png" alt="Main Window" width="400"/>

## Overview

VMUpdater uses VMware's `vmrun` CLI tool to headlessly boot a target VM, verify network connectivity, and execute a full system package upgrade (`yay -Syu`) — then cleanly shuts the VM back down. The entire process runs on a configurable weekly schedule and lives quietly in your system tray.

## Features

- **Scheduled Updates** — Set a recurring day and time (e.g., *Every Sunday at 3:00 AM*) for fully automated updates.
- **Manual Trigger** — Force an immediate update at any time via the *Update Now* button.
- **Headless Execution** — The VM boots without a GUI (`nogui`) and shuts down automatically after the update completes.
- **Network Validation** — Pings `8.8.8.8` from inside the guest before running updates. Aborts cleanly if the network is unavailable.
- **Keyring Safety** — Updates the Arch Linux keyring before the main upgrade to prevent GPG signature failures.
- **Progress Tracking** — A status bar and progress indicator keep you informed throughout the update process.
- **Persistent Configuration** — Your VMX path, credentials, and schedule are saved automatically between sessions.
- **System Tray Integration** — Minimizes to the system tray with a tooltip showing the last successful update time.
- **Activity Log** — An in-app log tab and timestamped log files (stored in `Logs\`) provide a full audit trail.

## Requirements

- Windows 10/11
- [VMware Workstation](https://www.vmware.com/products/workstation-pro.html) installed at the default path:
  ```
  C:\Program Files (x86)\VMware\VMware Workstation\vmrun.exe
  ```
- A VMware Workstation VM configured with a `.vmx` file
- Guest OS with `yay` and `pacman` available (Arch Linux or derivative)
- Guest credentials with `sudo` privileges

## Getting Started

1. **Clone the repository**
   ```bash
   git clone https://github.com/atshaw1994/VMUpdater.git
   ```

2. **Open in Visual Studio 2026** and build the solution.

3. **Configure the app:**
   - Browse to your VM's `.vmx` file
   - Enter your guest OS username and password
   - Select a day and time for the weekly scheduled update

4. Settings are saved automatically — just minimize to the tray and let it run.

## Project Structure

```
VMUpdater/
├── Views/
│   ├── MainWindow.xaml       # Main UI (Settings tab + Log tab)
│   ├── LogWindow.xaml        # Standalone log viewer
│   └── TimePicker.xaml       # Custom time picker control
├── ViewModels/
│   ├── MainViewModel.cs      # Core application logic & commands
│   └── ViewModelBase.cs      # INotifyPropertyChanged base
├── Helpers/
│   ├── RelayCommand.cs
│   ├── BooleanToVisibilityConverter.cs
│   ├── InverseBooleanConverter.cs
│   └── DateTimeToPartsConverter.cs
└── Properties/
    └── Settings.settings     # Persistent user configuration
```

## Tech Stack

- **.NET 10** / **WPF**
- **MVVM** architecture
- [H.NotifyIcon.Wpf](https://github.com/HavenDV/H.NotifyIcon) — system tray support

## License

This project is open source. See [LICENSE](LICENSE) for details.
