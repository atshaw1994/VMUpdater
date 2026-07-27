# <img src="VMUpdater.ico" alt="Icon" width="32"/> VMUpdater

This branch is for implementing the ability to enter custom hypervisors via locating their respective hypervisor binary tool. 

## Requirements

- Windows 10/11

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


## License

This project is licensed under the **GNU General Public License v3.0**. See [LICENSE](LICENSE) for the full license text.

You are free to use, modify, and distribute this software under the terms of the GPL v3. Any derivative works must also be distributed under the same license.
