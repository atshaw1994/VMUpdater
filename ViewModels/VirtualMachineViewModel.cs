using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using VMUpdater.Helpers;
using VMUpdater.Models;
using VMUpdater.Services;

namespace VMUpdater.ViewModels
{
    public partial class VirtualMachineViewModel : ObservableObject
    {
        private readonly Action<VirtualMachineViewModel> _onExpanded;
        public Action<VirtualMachineViewModel, bool>? RequestStartUpdate;
        public VirtualMachineModel Model { get; }
        private readonly VirtualMachineService _vmService;

        public VirtualMachineViewModel(VirtualMachineModel model, VirtualMachineService vmService, Action<VirtualMachineViewModel> onExpanded)
        {
            Model = model;
            _vmService = vmService;
            _onExpanded = onExpanded;

            _hypervisorType = Model.Hypervisor;
            _guestOSType = Model.GuestOSType;
            _displayName = "New Virtual Machine";
            _username = Model.Username;
            _password = Model.Password;
            _scheduleDay = Model.ScheduleDay;
            _scheduleTime = Model.ScheduleTime;
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
        private HypervisorType _hypervisorType;

        partial void OnHypervisorTypeChanged(HypervisorType value)
        {
            Model.Hypervisor = value;
        }

        [ObservableProperty]
        private string _guestOSType = string.Empty;

        partial void OnGuestOSTypeChanged(string value)
        {
            Model.GuestOSType = value;
            _vmService?.SaveVirtualMachineEntry(Model);
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
                    _vmService?.SaveVirtualMachineEntry(Model);
                }
            }
        }

        [ObservableProperty]
        private string _displayName = string.Empty;

        [ObservableProperty]
        private string _username = string.Empty;

        partial void OnUsernameChanged(string value)
        {
            Model.Username = value;
            _vmService?.SaveVirtualMachineEntry(Model);
        }

        [ObservableProperty]
        private string _password = string.Empty;

        partial void OnPasswordChanged(string value)
        {
            Model.Password = value;
            _vmService?.SaveVirtualMachineEntry(Model);
        }

        [ObservableProperty]
        private string _scheduleDay = string.Empty;

        partial void OnScheduleDayChanged(string value)
        {
            Model.ScheduleDay = value;
            CalculateNextScheduledUpdate();
            _vmService?.SaveVirtualMachineEntry(Model);
        }

        [ObservableProperty]
        private DateTime _scheduleTime;

        partial void OnScheduleTimeChanged(DateTime value)
        {
            Model.ScheduleTime = value;
            CalculateNextScheduledUpdate();
            _vmService?.SaveVirtualMachineEntry(Model);
        }

        [ObservableProperty]
        private bool _isExpanded;

        partial void OnIsExpandedChanged(bool value)
        {
            ExpandedIcon = value ? "\uE70E" : "\uE70D";
            if (value) _onExpanded?.Invoke(this);
        }

        [ObservableProperty]
        private string _expandedIcon = "\uE70D";

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
