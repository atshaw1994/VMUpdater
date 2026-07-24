using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using VMUpdater.Models;
using VMUpdater.Services.Abstractions;

namespace VMUpdater.ViewModels
{
    public partial class VirtualMachineViewModel : ObservableObject
    {
        private readonly Action<VirtualMachineViewModel> _onExpanded;
        public Action<VirtualMachineViewModel, bool>? RequestStartUpdate;
        public VirtualMachineModel Model { get; }
        private readonly IVirtualMachineService _vmService;
        private readonly IVirtualMachineRepository _repository;

        public VirtualMachineViewModel(
            VirtualMachineModel model,
            IVirtualMachineService vmService,
            IVirtualMachineRepository repository,
            Action<VirtualMachineViewModel> onExpanded)
        {
            Model = model;
            _vmService = vmService;
            _repository = repository;
            _onExpanded = onExpanded;

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
        public void BrowseForVirtualMachineFile()
        {
            Microsoft.Win32.OpenFileDialog dialog = new();
            if (Model.Hypervisor == HypervisorType.VirtualBox)
            {
                dialog.Filter = "VirtualBox VM Files (*.vbox)|*.vbox";
                dialog.Title = "Select a VirtualBox VM File";
            }
            else if (Model.Hypervisor == HypervisorType.QEMU)
            {
                dialog.Filter = "QEMU Configuration (*.qemu)|*.qemu";
                dialog.Title = "Select QEMU Configuration Target File";
            }
            else if (Model.Hypervisor == HypervisorType.VMWare)
            {
                dialog.Filter = "VMware Configuration (*.vmx)|*.vmx";
                dialog.Title = "Select Virtual Machine VMX Configuration Target File";
            }

            if (dialog.ShowDialog() == true) VMPath = dialog.FileName;
        }

        #endregion

        #region Properties

        [ObservableProperty]
        public partial HypervisorType HypervisorType { get; set; }

        partial void OnHypervisorTypeChanged(HypervisorType value)
        {
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
    }
}