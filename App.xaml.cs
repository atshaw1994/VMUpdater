using H.NotifyIcon;
using System;
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
            // 1. Create single ViewModel instance
            _viewModel = new MainViewModel();

            // 2. Set up the Background Scheduler
            _schedulerTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _schedulerTimer.Tick += BackgroundSchedulerLoop_Tick;
            _schedulerTimer.Start();

            // 3. Initialize MainWindow
            _mainWindow = new MainWindow(_viewModel);
            MainWindow = _mainWindow;

            // 4. Construct TaskbarIcon in code (Prevents OpenConsole.exe & App.xaml parsing crashes)
            InitializeTrayIcon();

            // 5. Tooltip Push Model
            if (_notifyIcon != null)
            {
                _notifyIcon.ToolTipText = _viewModel.TrayToolTipText;

                _viewModel.OnTooltipRefreshRequested = (newTooltip) =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _notifyIcon?.ToolTipText = newTooltip;
                    }));
                };
            }
        }

        private void InitializeTrayIcon()
        {
            if (_viewModel == null) return;

            // Build Context Menu in code
            var contextMenu = new ContextMenu();

            var menuOpen = new MenuItem { Header = "Open", Command = _viewModel.ShowMainWindowCommand };
            var menuOpenLog = new MenuItem { Header = "Open Log", Command = _viewModel.ShowLogCommand };
            var menuUpdateAll = new MenuItem { Header = "Update All", Command = _viewModel.UpdateAllCommand };
            var menuExit = new MenuItem { Header = "Exit", Command = _viewModel.ExitCommand };

            contextMenu.Items.Add(menuOpen);
            contextMenu.Items.Add(menuOpenLog);
            contextMenu.Items.Add(menuUpdateAll);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(menuExit);

            // Instantiate TaskbarIcon without using FindResource()
            _notifyIcon = new TaskbarIcon
            {
                IconSource = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri("pack://application:,,,/Resources/VMUpdater.ico")),
                ContextMenu = contextMenu,
                DataContext = _viewModel,
                DoubleClickCommand = _viewModel.ShowMainWindowCommand
            };

            // Register handle cleanly with Windows Shell
            _notifyIcon.ForceCreate();
        }

        private async void BackgroundSchedulerLoop_Tick(object? sender, EventArgs e)
        {
            if (_viewModel == null || _viewModel.IsUpdating) return;

            DateTime now = DateTime.Now;

            foreach (var vm in _viewModel.VirtualMachines)
            {
                // FIX: Use 'continue' instead of 'return' so subsequent VMs in the list get evaluated!
                if (!string.Equals(now.DayOfWeek.ToString(), vm.ScheduleDay, StringComparison.OrdinalIgnoreCase))
                    continue;

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
            _notifyIcon?.Dispose(); // Prevents lingering ghost icons in system tray
        }
    }
}