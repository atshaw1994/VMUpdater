using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using FluentAvalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using VMUpdater.Services;
using VMUpdater.Services.Abstractions;
using VMUpdater.Services.Orchestration;
using VMUpdater.ViewModels;
using VMUpdater.Views;

namespace VMUpdater
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;

        private DispatcherTimer? _schedulerTimer;
        private MainViewModel? _viewModel;

        public override void OnFrameworkInitializationCompleted()
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            Services = serviceCollection.BuildServiceProvider();

            _viewModel = Services.GetRequiredService<MainViewModel>();
            DataContext = _viewModel; // enables TrayIcon XAML bindings

            var faTheme = new FluentAvaloniaTheme
            {
                PreferUserAccentColor = true
            };
            Styles.Add(faTheme);

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                desktop.MainWindow = Services.GetRequiredService<MainWindow>();
                desktop.MainWindow.Show();
                desktop.Exit += (_, _) => _schedulerTimer?.Stop();
            }

            _schedulerTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _schedulerTimer.Tick += BackgroundSchedulerLoop_Tick;
            _schedulerTimer.Start();

            base.OnFrameworkInitializationCompleted();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IVirtualMachineRepository, JsonVirtualMachineRepository>();
            services.AddSingleton<IHypervisorRepository, JsonHypervisorRepository>();
            services.AddSingleton<IGuestOSRepository, JsonGuestOSRepository>();
            services.AddSingleton<GenericHypervisorUpdater>();
            services.AddTransient<IVirtualMachineService, VirtualMachineService>();
            services.AddSingleton<MainServicesContext>();
            services.AddSingleton<MainViewModel>();
            services.AddTransient(provider => new MainWindow(
                provider.GetRequiredService<MainViewModel>()
            ));
        }

        private async void BackgroundSchedulerLoop_Tick(object? sender, EventArgs e)
        {
            if (_viewModel == null || _viewModel.IsUpdating) return;

            DateTime now = DateTime.Now;

            foreach (var vm in _viewModel.VirtualMachines)
            {
                if (!string.Equals(now.DayOfWeek.ToString(), vm.ScheduleDay, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (_viewModel.IsAutoMode && vm.IsAutoUpdate)
                {
                    if (vm.Model.NextUpdate != DateTime.MinValue &&
                        now.Hour == vm.Model.NextUpdate.Hour &&
                        now.Minute == vm.Model.NextUpdate.Minute)
                    {
                        _viewModel.LogMessage($"Automated Cron Schedule validated for [{vm.DisplayName}]. Requesting update...");
                        _viewModel.EnqueueUpdateRequest(vm, forceUpdate: false);
                    } 
                }
            }
        }
    }
}