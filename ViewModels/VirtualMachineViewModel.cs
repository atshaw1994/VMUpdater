using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using VMUpdater.Models;
using VMUpdater.Views;
using VMUpdater.Services.Abstractions;
using System.Windows;
using VMUpdater.Services.Orchestration;

namespace VMUpdater.ViewModels
{
    public record VMViewModelContext(
        IVirtualMachineService VmService,
        IVirtualMachineRepository Repository,
        IHypervisorRepository HypervisorRepository,
        IGuestOSRepository GuestOSRepository,
        ObservableCollection<HypervisorModel> Hypervisors,
        ObservableCollection<GuestOSModel> GuestOSTypes,
        Action<VirtualMachineViewModel> OnExpanded
    );

    public partial class VirtualMachineViewModel : ObservableObject
    {
        private readonly VMViewModelContext _ctx;

        public Action<VirtualMachineViewModel, bool>? RequestStartUpdate;
        public VirtualMachineModel Model { get; }

        public ObservableCollection<GuestOSModel> GuestOSTypes => _ctx.GuestOSTypes;
        public ObservableCollection<HypervisorModel> Hypervisors => _ctx.Hypervisors;

        public VirtualMachineViewModel(VirtualMachineModel model, VMViewModelContext context)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            _ctx = context ?? throw new ArgumentNullException(nameof(context));

            // Ensure Sentinel Items exist
            if (!Hypervisors.Any(h => h.Name == AddNewHypervisorSentinel.Name))
                Hypervisors.Add(AddNewHypervisorSentinel);

            if (!GuestOSTypes.Any(os => os.Name == AddNewGuestOSSentinel.Name))
            {
                // Seed defaults if list is empty
                if (GuestOSTypes.Count == 0)
                {
                    GuestOSTypes.Add(DefaultGuestOSTypes.Ubuntu);
                    GuestOSTypes.Add(DefaultGuestOSTypes.Arch);
                    GuestOSTypes.Add(DefaultGuestOSTypes.Windows);
                }
                GuestOSTypes.Add(AddNewGuestOSSentinel);
            }

            // Initialize bindable properties from model
            DisplayName = !string.IsNullOrEmpty(Model.VMPath)
                ? Path.GetFileNameWithoutExtension(Model.VMPath)
                : "New Virtual Machine";

            Username = Model.Username;
            Password = Model.Password;
            ScheduleDay = Model.ScheduleDay;
            ScheduleTime = Model.ScheduleTime;

            // Async non-blocking load for repository lookups
            _ = InitializeSelectionAsync();
        }

        private async Task InitializeSelectionAsync()
        {
            var hypervisor = await _ctx.HypervisorRepository.GetByIdAsync(Model.HypervisorId)
                             ?? new HypervisorModel();

            var guestOS = await _ctx.GuestOSRepository.GetByIdAsync(Model.GuestOSId)
                          ?? DefaultGuestOSTypes.Windows;

            HypervisorType = Hypervisors.FirstOrDefault(h => h.Id == hypervisor.Id) ?? hypervisor;
            GuestOSType = GuestOSTypes.FirstOrDefault(os => os.Id == guestOS.Id) ?? guestOS;
        }

        #region Commands

        [RelayCommand]
        private void ToggleExpand() => IsExpanded = !IsExpanded;

        [RelayCommand]
        private void UpdateNow() => RequestStartUpdate?.Invoke(this, true);

        [RelayCommand]
        public void Browse()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select a Virtual Machine File"
            };

            if (dialog.ShowDialog() == true) VMPath = dialog.FileName;
        }

        [RelayCommand]
        private async Task AddHypervisorAsync()
        {
            var dialog = new AddNewHypervisorView { Owner = Application.Current?.MainWindow };
            var currentHypervisor = HypervisorType;

            if (dialog.ShowDialog() == true && dialog.DataContext is AddHypervisorViewModel vm)
            {
                HypervisorModel newHypervisor = vm.CreatedHypervisor;
                await _ctx.HypervisorRepository.SaveAsync(newHypervisor);

                int sentinelIndex = Hypervisors.IndexOf(AddNewHypervisorSentinel);
                if (sentinelIndex >= 0)
                    Hypervisors.Insert(sentinelIndex, newHypervisor);
                else
                    Hypervisors.Add(newHypervisor);

                HypervisorType = newHypervisor;
            }
            else
            {
                HypervisorType = currentHypervisor;
            }
        }

        [RelayCommand]
        private async Task AddGuestOSAsync()
        {
            var dialog = new AddGuestOSView { Owner = Application.Current?.MainWindow };
            var currentGuestOS = GuestOSType;

            if (dialog.ShowDialog() == true && dialog.DataContext is AddGuestOSViewModel vm)
            {
                GuestOSModel newGuestOS = vm.CreatedGuestOS;
                await _ctx.GuestOSRepository.SaveAsync(newGuestOS);

                int sentinelIndex = GuestOSTypes.IndexOf(AddNewGuestOSSentinel);
                if (sentinelIndex >= 0)
                    GuestOSTypes.Insert(sentinelIndex, newGuestOS);
                else
                    GuestOSTypes.Add(newGuestOS);

                GuestOSType = newGuestOS;
            }
            else
            {
                GuestOSType = currentGuestOS;
            }
        }

        #endregion

        #region Properties

        [ObservableProperty]
        public partial bool IsUpdating { get; set; }

        [ObservableProperty]
        public partial double UpdateProgress { get; set; }

        [ObservableProperty]
        public partial HypervisorModel HypervisorType { get; set; } = new();

        partial void OnHypervisorTypeChanging(HypervisorModel value)
        {
            if (value == AddNewHypervisorSentinel)
            {
                AddHypervisorCommand.Execute(null);
                OnPropertyChanged(nameof(HypervisorType));
            }
        }

        partial void OnHypervisorTypeChanged(HypervisorModel value)
        {
            if (value == AddNewHypervisorSentinel || value == null)
                return;

            Model.HypervisorId = value.Id;
            _ = SaveAsync();
        }

        [ObservableProperty]
        public partial GuestOSModel GuestOSType { get; set; } = DefaultGuestOSTypes.Windows;

        partial void OnGuestOSTypeChanging(GuestOSModel value)
        {
            if (value == AddNewGuestOSSentinel)
            {
                AddGuestOSCommand.Execute(null);
                OnPropertyChanged(nameof(GuestOSType));
            }
        }

        partial void OnGuestOSTypeChanged(GuestOSModel value)
        {
            if (value == AddNewGuestOSSentinel || value == null)
                return;

            Model.GuestOSId = value.Id;
            _ = SaveAsync();
        }

        public string VMPath
        {
            get => Model.VMPath;
            set
            {
                if (SetProperty(Model.VMPath, value, Model, (m, val) => m.VMPath = val))
                {
                    DisplayName = !string.IsNullOrEmpty(value) ? Path.GetFileNameWithoutExtension(value) : "New Virtual Machine";
                    _ = SaveAsync();
                }
            }
        }

        [ObservableProperty]
        public partial string DisplayName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Username { get; set; } = string.Empty;

        partial void OnUsernameChanged(string value)
        {
            Model.Username = value;
            _ = SaveAsync();
        }

        [ObservableProperty]
        public partial string Password { get; set; } = string.Empty;

        partial void OnPasswordChanged(string value)
        {
            Model.Password = value;
            _ = SaveAsync();
        }

        [ObservableProperty]
        public partial string ScheduleDay { get; set; } = string.Empty;

        partial void OnScheduleDayChanged(string value)
        {
            Model.ScheduleDay = value;
            CalculateNextScheduledUpdate();
            _ = SaveAsync();
        }

        [ObservableProperty]
        public partial DateTime ScheduleTime { get; set; }

        partial void OnScheduleTimeChanged(DateTime value)
        {
            Model.ScheduleTime = value;
            CalculateNextScheduledUpdate();
            _ = SaveAsync();
        }

        [ObservableProperty]
        public partial bool IsExpanded { get; set; }

        partial void OnIsExpandedChanged(bool value)
        {
            ExpandedIcon = value ? "\uE70E" : "\uE70D";
            if (value) _ctx.OnExpanded?.Invoke(this);
        }

        [ObservableProperty]
        public partial string ExpandedIcon { get; set; } = "\uE70D";

        public DateTime LastUpdate
        {
            get => Model.LastUpdate;
            set
            {
                if (SetProperty(Model.LastUpdate, value, Model, (m, val) => m.LastUpdate = val))
                {
                    OnPropertyChanged(nameof(LastUpdateDisplayText));
                }
            }
        }

        public string LastUpdateDisplayText =>
            Model.LastUpdate == DateTime.MinValue
                ? "Last Update: Never"
                : $"Last Update: {Model.LastUpdate:dddd, MMMM d 'at' hh:mm tt}";

        public string NextUpdateDisplayText =>
            Model.NextUpdate == DateTime.MinValue
                ? "Next Update: Never"
                : $"Next Update: {Model.NextUpdate:dddd, MMMM d 'at' hh:mm tt}";

        #endregion

        private async Task SaveAsync()
        {
            if (_ctx.Repository != null)
            {
                await _ctx.Repository.SaveAsync(Model);
            }
        }

        public void CalculateNextScheduledUpdate()
        {
            if (string.IsNullOrEmpty(Model.ScheduleDay) || Model.ScheduleTime == DateTime.MinValue) return;

            if (Enum.TryParse(Model.ScheduleDay, true, out DayOfWeek targetDay))
            {
                DateTime now = DateTime.Now;
                DateTime timeTarget = Model.ScheduleTime;
                DateTime calculated = new(now.Year, now.Month, now.Day, timeTarget.Hour, timeTarget.Minute, 0);

                while (calculated.DayOfWeek != targetDay) calculated = calculated.AddDays(1);
                if (calculated < now) calculated = calculated.AddDays(7);

                Model.NextUpdate = calculated;
                OnPropertyChanged(nameof(NextUpdateDisplayText));
            }
        }

        private static readonly HypervisorModel AddNewHypervisorSentinel = new() { Name = "+ Add New Hypervisor..." };
        private static readonly GuestOSModel AddNewGuestOSSentinel = new() { Name = "+ Add New Guest OS..." };
    }
}