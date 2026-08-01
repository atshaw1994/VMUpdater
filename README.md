# <img src="VMUpdater.ico" alt="Icon" width="32"/> VMUpdater

This branch's purpose is to convert the project to use Avalonia UI instead of WPF to allow cross-platform compatibility. The project is still in development, and some features may not be fully implemented yet.

## Avalonia Migration Todo List
### :heavy_check_mark: **Phase 1 — Project Setup**
### :heavy_check_mark: **Phase 2 — App Entry Point & Bootstrapping**
### :heavy_check_mark: **Phase 3 — Helpers & Converters**
### :heavy_check_mark: **Phase 4 — TimePicker UserControl (highest complexity)**
### :heavy_check_mark: **Phase 5 — Views (XAML)**
### :arrow_right: **Phase 6 — ViewModels**
 - :heavy_check_mark: **Update MainViewModel.cs:**
 - :arrows_counterclockwise: **Update VirtualMachineViewModel.cs:**
	- Replace ```Microsoft.Win32.OpenFileDialog``` with StorageProvider API
	- Replace AddNewHypervisorView/AddGuestOSView ```ShowDialog()``` calls with ```window.ShowDialog<T>(owner)``` returning ```Task<T>```
	- Replace ```dialog.DialogResult == true``` check with typed return value
 - **Update AddGuestOSViewModel.cs and AddHypervisorViewModel.cs:**
	- Remove ```Window``` parameter from Save commands; introduce ```ICloseable``` interface or close via ```Window.Close(result)``` pattern
	- Replace ```window.DialogResult = true``` with ```window.Close(true)```
	- Replace ```Microsoft.Win32.OpenFileDialog``` (in AddHypervisorViewModel) with StorageProvider API
### **Phase 7 — Assets & Resources**
 - **Update VMUpdater.ico / tray icon reference**
	- from ```pack://application:,,,/Resources/VMUpdater.ico``` to ```avares://VMUpdater/Resources/VMUpdater.ico```
 - **Ensure icon/image assets are marked as ```AvaloniaResource``` in the .csproj**
 - **Bundle ```Segoe Fluent Icons``` font as an embedded asset, or replace with ```FluentIcons.Avalonia NuGet```**
### **Phase 8 — Verification**
 - Build project and resolve any remaining compile errors
 - Run on Windows and verify UI renders correctly with Fluent theme
 - Test on Linux/macOS (or a Linux Docker container) to verify cross-platform functionality
 - Test tray icon behavior via H.NotifyIcon on each target platform
 - Test all dialogs (Add Hypervisor, Add Guest OS, About) open and close correctly with the new ShowDialog<T>() pattern

## License

This project is licensed under the **GNU General Public License v3.0**. See [LICENSE](LICENSE) for the full license text.

You are free to use, modify, and distribute this software under the terms of the GPL v3. Any derivative works must also be distributed under the same license.
