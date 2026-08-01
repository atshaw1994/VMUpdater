# <img src="VMUpdater.ico" alt="Icon" width="32"/> VMUpdater

This branch's purpose is to convert the project to use Avalonia UI instead of WPF to allow cross-platform compatibility. The project is still in development, and some features may not be fully implemented yet.

## Avalonia Migration Todo List
### :heavy_check_mark: **Phase 1 — Project Setup**
### :heavy_check_mark: **Phase 2 — App Entry Point & Bootstrapping**
### :arrows_counterclockwise:**Phase 3 — Helpers & Converters**
 - **Rewrite BooleanToVisibilityConverter.cs:** 
	- Implement ```IValueConverter``` from ```Avalonia.Data.Converters```
	- Return ```bool``` instead of ```Visibility enum``` (or delete and use built-in ```BoolConverters```)
### **Phase 4 — TimePicker UserControl (highest complexity)**
 - **Rewrite TimePicker.xaml.cs:**
	- replace all 4 ```DependencyProperty.Register``` / ```FrameworkPropertyMetadata``` declarations with ```AvaloniaProperty.Register<TimePicker, T>()``` (styled or direct properties)
	- update ```DependencyPropertyChangedEventArgs``` callbacks to Avalonia property-changed signatures
	- change base class to ```Avalonia.Controls.UserControl```
 - **Rewrite TimePicker.xaml:**
	- update namespace declarations to ```Avalonia```
	- verify ```ElementName=Root``` binding still works
### **Phase 5 — Views (XAML)**
 - **Rewrite MainWindow.xaml:**
 	- Change root to ```Avalonia Window```
	- remove ```ThemeMode="System"```
	- add ```RequestedThemeVariant```
 	- Replace all WPF Fluent DynamicResource brush keys with Avalonia theme resource keys
 	- Replace ```StatusBar```/```StatusBarItem``` with a ```Grid```/```StackPanel``` footer
 	- Replace ApplicationCommands.Cut/Copy/Paste — remove from Menu; Avalonia TextBox handles these natively
 	- Replace ```Style.Triggers/DataTrigger``` row-expand animation with Avalonia Animation or Transition on height
 	- Replace App.xaml ProgressBar ControlTemplate + Storyboard with Avalonia control theme
 	- Remove ```TextOptions.*``` / ```RenderOptions.*``` attached properties
 	- Replace ```DataTemplate DataType="{x:Type ...}"``` with Avalonia DataType syntax
 	- Replace ```ItemsControlWithScrollBarMargin``` DataTrigger (ancestor ScrollViewer binding) with an Avalonia equivalent
 	- Replace ```Segoe Fluent Icons Unicode``` glyphs with bundled asset or ```FluentIcons.Avalonia``` glyphs
 - **Rewrite VirtualMachineEntry.xaml:**
 	- Replace all DynamicResource Fluent brush keys
 	- Replace ```Style.Triggers/DataTrigger``` for DetailsPanel visibility with Avalonia style selectors or IsVisible binding
 	- Fix ```SelectedValuePath="Content"``` on ComboBox (use ```SelectedItem``` binding or ```SelectedValueBinding```)
 	- Remove WPF design-time ```.``` attributes not supported by Avalonia (or update to Avalonia equivalents)
 - **Rewrite AddGuestOSView.xaml and AddNewHypervisorView.xaml: **
	- change Window root to Avalonia
	- verify ```IsDefault```/```IsCancel```, ```SizeToContent```, ```WindowStartupLocation```, ```ResizeMode```, ```ShowInTaskbar``` map correctly
 - **Rewrite AboutDialog.xaml:**
	- change Window root
	- replace ThemeMode
	- replace DynamicResource brush keys
	- fix CornerRadius on Button (set directly or via style)
 - **Update all code-behind files:** 
	- change ```using System.Windows.*``` to ```using Avalonia.*``` equivalents
	- update ```RoutedEventArgs```
	- update ```TextChangedEventArgs```
	- update ```Window.Closing``` → ```WindowClosingEventArgs```
### **Phase 6 — ViewModels**
 - **Update MainViewModel.cs:**
	- Replace ```ICollectionView``` / ```CollectionViewSource``` / ```BindingOperations``` with a FilteredVMs ObservableCollection computed via LINQ
	- Remove ```BindingOperations.EnableCollectionSynchronization```
	- Replace ```Microsoft.Win32.OpenFileDialog``` / ```SaveFileDialog``` with ```StorageProvider.OpenFilePickerAsync``` / ```SaveFilePickerAsync``` (inject ```IStorageProvider``` or use a file picker service)
	- Replace ```Application.Current?.Shutdown()``` with ```(Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown()```
	- Replace ```Application.Current?.MainWindow``` references with injected window reference or ```IClassicDesktopStyleApplicationLifetime.MainWindow```
 - **Update VirtualMachineViewModel.cs:**
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
