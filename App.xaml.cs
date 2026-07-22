using H.NotifyIcon;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using VMUpdater.ViewModels;
using VMUpdater.Views;

namespace VMUpdater
{
    public partial class App : Application
    {
        private DispatcherTimer? _schedulerTimer;
        private MainViewModel? _viewModel;
        private MainWindow? _mainWindow;
        private TaskbarIcon? _notifyIcon;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Create our single, shared ViewModel instance
            _viewModel = new MainViewModel();

            // Set up the Background Scheduler
            _schedulerTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _schedulerTimer.Tick += BackgroundSchedulerLoop_Tick;
            _schedulerTimer.Start();

            // Initialize MainWindow but keep it hidden
            _mainWindow = new MainWindow(_viewModel);
            MainWindow = _mainWindow;

            // Force-resolve the TaskbarIcon from Resources
            _notifyIcon = (TaskbarIcon)FindResource("NotifyIcon");

            // This forces WPF to instantiate the Win32 window handles 
            // for the NotifyIcon and registers it with the Windows shell!
            _notifyIcon.ForceCreate();

            // Explicitly wire the DataContext and bindings for the context menu
            _notifyIcon.DataContext = _viewModel;
            ConfigureTrayBindingsAndEvents();

            // Wire up the tooltip push model: Set the initial tooltip string
            _notifyIcon.ToolTipText = _viewModel.TrayToolTipText;

            // Listen for future runtime modifications and push them to the native property
            _viewModel.OnTooltipRefreshRequested = (newTooltip) =>
            {
                // Safety marshal to ensure UI thread assignment
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _notifyIcon?.ToolTipText = newTooltip;
                }));
            };
        }

        private void ConfigureTrayBindingsAndEvents()
        {
            if (_notifyIcon == null || _viewModel == null) return;

            // Wire up double click to restore window
            _notifyIcon.DoubleClickCommand = _viewModel.ShowMainWindowCommand;

            var contextMenu = _notifyIcon.ContextMenu;
            if (contextMenu != null)
            {
                var menuOpen = contextMenu.Template.FindName("MenuOpen", contextMenu) as MenuItem
                               ?? FindMenuItemByName(contextMenu, "MenuOpen");
                var menuOpenLog = contextMenu.Template.FindName("MenuOpenLog", contextMenu) as MenuItem
                                  ?? FindMenuItemByName(contextMenu, "MenuOpenLog");
                var menuExit = contextMenu.Template.FindName("MenuExit", contextMenu) as MenuItem
                               ?? FindMenuItemByName(contextMenu, "MenuExit");

                // Bind Commands
                menuOpen?.Command = _viewModel.ShowMainWindowCommand;
                menuOpenLog?.Command = _viewModel.ShowLogCommand;
                menuExit?.Command = _viewModel.ExitCommand;
            }
        }

        // Helper to find named controls inside context menus if templating hasn't fully loaded yet
        private static MenuItem? FindMenuItemByName(ContextMenu menu, string name)
        {
            foreach (var item in menu.Items)
            {
                if (item is MenuItem menuItem && menuItem.Name == name)
                    return menuItem;
            }
            return null;
        }

        private async void BackgroundSchedulerLoop_Tick(object? sender, EventArgs e)
        {
            if (_viewModel == null || _viewModel.IsUpdating) return;

            DateTime now = DateTime.Now;

            foreach (var vm in _viewModel.VirtualMachines)
            {
                if (!string.Equals(now.DayOfWeek.ToString(), vm.ScheduleDay, StringComparison.OrdinalIgnoreCase))
                    return;

                if (vm.Model.NextUpdate != DateTime.MinValue &&
                    now.Hour == vm.Model.NextUpdate.Hour &&
                    now.Minute == vm.Model.NextUpdate.Minute)
                {
                    _viewModel.LogMessage($"Automated Cron Schedule validated for [{vm.DisplayName}]. Requesting update...");
                    _viewModel.EnqueueUpdateRequest(vm, forceUpdate: false);
                }
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            _schedulerTimer?.Stop();
            _notifyIcon?.Dispose(); // Disposes cleanly so no ghost icon remains
        }
    }
}