using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using VMUpdater.Models;
using VMUpdater.Views;
using VMUpdater.Services.Abstractions;
using System.Windows;

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
        public ObservableCollection<string> GuestOSTypes { get; }
        public ObservableCollection<HypervisorModel> Hypervisors { get; }

        public VirtualMachineViewModel(
            VirtualMachineModel model,
            IVirtualMachineService vmService,
            IVirtualMachineRepository repository,
            IHypervisorRepository hypervisorRepository,
            Action<VirtualMachineViewModel> onExpanded)
        {
            Model = model;
            _vmService = vmService;
            _repository = repository;
            _onExpanded = onExpanded;
            _hypervisorRepository = hypervisorRepository;
            Hypervisors = [];

            _ = InitializeHypervisors();

            GuestOSTypes = [
                "Ubuntu", "Debian Linux", "Arch Linux", "Fedora", "Red Hat", "openSUSE", "Alpine", "macOS", "Windows"
            ];

            HypervisorType = Model.Hypervisor;
            GuestOSType = Model.GuestOSType;
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
            var dialog = new AddNewHypervisorView
            {
                Owner = Application.Current?.MainWindow
            };

            if (dialog.ShowDialog() == true && dialog.DataContext is AddHypervisorViewModel vm)
            {
                HypervisorModel newHypervisor = vm.CreatedHypervisor;

                // Save to repository
                await _hypervisorRepository.SaveAsync(newHypervisor);

                // Insert new hypervisor right before the sentinel item at the bottom
                int sentinelIndex = Hypervisors.IndexOf(AddNewSentinel);
                if (sentinelIndex >= 0)
                {
                    Hypervisors.Insert(sentinelIndex, newHypervisor);
                }
                else
                {
                    Hypervisors.Add(newHypervisor);
                }
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
            if (value == AddNewSentinel)
            {
                AddHypervisorCommand.Execute(null);
                OnPropertyChanged(nameof(HypervisorType));
            }
        }

        partial void OnHypervisorTypeChanged(HypervisorModel value)
        {
            // Ignore sentinel if it somehow slips through
            if (value == AddNewSentinel || value == null)
                return;

            Model.Hypervisor = value;
            _ = SaveAsync();
        }

        [ObservableProperty]
        public partial string GuestOSType { get; set; } = string.Empty;

        partial void OnGuestOSTypeChanged(string value)
        {
            Model.GuestOSType = value;
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

        private async Task InitializeHypervisors()
        {
            var hypervisors = await _hypervisorRepository.LoadAllAsync();

            // Ensure collection updates are on the UI thread
            Application.Current?.Dispatcher.Invoke(() =>
            {
                Hypervisors.Clear();
                foreach (var hypervisor in hypervisors)
                {
                    Hypervisors.Add(hypervisor);
                }
                Hypervisors.Add(AddNewSentinel);

                if (Model.Hypervisor != null)
                {
                    var matchingHypervisor = Hypervisors.FirstOrDefault(h => h.Id == Model.Hypervisor.Id);
                    if (matchingHypervisor != null)
                    {
                        // Assigning this forces WPF to recognise the matching object in Hypervisors collection
                        HypervisorType = matchingHypervisor;
                    }
                }
            });
        }

        private readonly HypervisorModel AddNewSentinel = new() { Name = "+ Add New Hypervisor..." };
    }
}