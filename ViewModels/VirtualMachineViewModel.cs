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
    public partial class VirtualMachineViewModel : ObservableObject
    {
        private readonly Action<VirtualMachineViewModel> _onExpanded;
        public Action<VirtualMachineViewModel, bool>? RequestStartUpdate;
        public VirtualMachineModel Model { get; }
        private readonly IVirtualMachineService _vmService;
        private readonly IVirtualMachineRepository _repository;
        private readonly IHypervisorRepository _hypervisorRepository;
        private readonly IGuestOSRepository _guestOSRepository;
        public ObservableCollection<GuestOSModel> GuestOSTypes { get; }
        public ObservableCollection<HypervisorModel> Hypervisors { get; }

        public VirtualMachineViewModel(
            VirtualMachineModel model,
            IVirtualMachineService vmService,
            IVirtualMachineRepository repository,
            IHypervisorRepository hypervisorRepository,
            IGuestOSRepository guestOSRepository,
            ObservableCollection<HypervisorModel> hypervisors,
            ObservableCollection<GuestOSModel> guestOSTypes,
            Action<VirtualMachineViewModel> onExpanded)
        {
            Model = model;
            _vmService = vmService;
            _repository = repository;
            _onExpanded = onExpanded;
            _hypervisorRepository = hypervisorRepository;
            _guestOSRepository = guestOSRepository;
            Hypervisors = hypervisors;
            GuestOSTypes = guestOSTypes;

            if (!Hypervisors.Any(h => h.Name == AddNewHypervisorSentinel.Name))
                Hypervisors.Add(AddNewHypervisorSentinel);

            if (!GuestOSTypes.Any(os => os.Name == AddNewGuestOSSentinel.Name))
                GuestOSTypes.Add(AddNewGuestOSSentinel);

            GuestOSTypes =
            [
                DefaultGuestOSTypes.Ubuntu,
                DefaultGuestOSTypes.Arch,
                DefaultGuestOSTypes.Windows
            ];

            var hypervisor = hypervisorRepository.GetByIdAsync(model.HypervisorId).Result;
            hypervisor ??= new HypervisorModel();

            var guestOS = guestOSRepository.GetByIdAsync(model.GuestOSId).Result;
            guestOS ??= DefaultGuestOSTypes.Windows;

            HypervisorType = Hypervisors.FirstOrDefault(h => h.Id == hypervisor.Id) ?? hypervisor;
            GuestOSType = GuestOSTypes.FirstOrDefault(os => os.Id == guestOS.Id) ?? guestOS;
            DisplayName = "New Virtual Machine";
            Username = Model.Username;
            Password = Model.Password;
            ScheduleDay = Model.ScheduleDay;
            ScheduleTime = Model.ScheduleTime;
        }

        #region Commands

        [RelayCommand]
        private void ToggleExpand() => IsExpanded = !IsExpanded;

        [RelayCommand]
        private void UpdateNow() => RequestStartUpdate?.Invoke(this, true);

        [RelayCommand]
        public void Browse()
        {
            Microsoft.Win32.OpenFileDialog dialog = new()
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

                // Save to repository
                await _hypervisorRepository.SaveAsync(newHypervisor);

                // Insert new hypervisor right before the sentinel item at the bottom
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

                // Save to repository
                await _guestOSRepository.SaveAsync(newGuestOS);

                // Insert new guest OS right before the sentinel item at the bottom
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
        public partial HypervisorModel HypervisorType { get; set; }

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
            // Ignore sentinel if it somehow slips through
            if (value == AddNewHypervisorSentinel || value == null)
                return;

            Model.HypervisorId = value.Id;
            _ = SaveAsync();
        }

        [ObservableProperty]
        public partial GuestOSModel GuestOSType { get; set; }
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
            // Ignore sentinel if it somehow slips through
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
                if (SetProperty(
                    Model.VMPath,
                    value,
                    Model,
                    (model, val) => model.VMPath = val))
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
            if (value) _onExpanded?.Invoke(this);
        }

        [ObservableProperty]
        public partial string ExpandedIcon { get; set; } = "\uE70D";

        public DateTime LastUpdate
        {
            get => Model.LastUpdate;
            set
            {
                if (SetProperty(
                    Model.LastUpdate,
                    value,
                    Model,
                    (model, val) => model.LastUpdate = val))
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
            if (_repository != null)
            {
                await _repository.SaveAsync(Model);
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

        private readonly HypervisorModel AddNewHypervisorSentinel = new() { Name = "+ Add New Hypervisor..." };

        private readonly GuestOSModel AddNewGuestOSSentinel = new() { Name = "+ Add New Guest OS..." };
    }
}