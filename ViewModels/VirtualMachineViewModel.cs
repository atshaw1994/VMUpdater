using System.IO;
using System.Windows.Input;
using VMUpdater.Helpers;
using VMUpdater.Models;
using VMUpdater.Services;

namespace VMUpdater.ViewModels
{
    public class VirtualMachineViewModel : ViewModelBase
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

            ToggleExpandCommand = new RelayCommand(ToggleExpand);
            UpdateNowCommand = new RelayCommand(() => RequestStartUpdate?.Invoke(this, true));
            BrowseCommand = new RelayCommand(BrowseForVirtualMachineFile);
        }

        #region Commands
        public ICommand ToggleExpandCommand { get; }
        public ICommand UpdateNowCommand { get; }
        public ICommand BrowseCommand { get; }
        #endregion

        #region Private Properties
        private HypervisorType _hypervisorType;
        private string _guestOSType;
        private string _displayName;
        private string _username;
        private string _password;
        private string _scheduleDay;
        private DateTime _scheduleTime;
        private bool _isExpanded;
        private string _expandedIcon = "\uE70D";
        #endregion

        #region Public Properties
        public HypervisorType HypervisorType
        {
            get => _hypervisorType;
            set
            {
                if (SetProperty(ref _hypervisorType, value, nameof(HypervisorType)))
                    Model.Hypervisor = value;
            }
        }
        public string GuestOSType
        {
            get => _guestOSType;
            set
            {
                if (SetProperty(ref _guestOSType, value, nameof(GuestOSType)))
                {
                    Model.GuestOSType = value;
                    _vmService!.SaveVirtualMachineEntry(Model);
                }
            }
        }
        public string VMPath
        {
            get => Model.VMPath;
            set
            {
                if (SetProperty(() => Model.VMPath, v => Model.VMPath = v, value))
                {
                    DisplayName =!string.IsNullOrEmpty(value) ? Path.GetFileNameWithoutExtension(value) : "New Virtual Machine";
                    _vmService!.SaveVirtualMachineEntry(Model);
                }
            }
        }
        public string DisplayName { get => _displayName; set => SetProperty(ref _displayName, value, nameof(DisplayName)); }
        public string Username
        {
            get => _username;
            set
            {
                if (SetProperty(ref _username, value, nameof(Username)))
                {
                    Model.Username = value;
                    _vmService!.SaveVirtualMachineEntry(Model);
                }
            }
        }
        public string Password
        {
            get => _password;
            set
            {
                if (SetProperty(ref _password, value, nameof(Password)))
                {
                    Model.Password = value;
                    _vmService!.SaveVirtualMachineEntry(Model);
                }
            }
        }
        public string ScheduleDay
        {
            get => _scheduleDay;
            set
            {
                if (SetProperty(ref _scheduleDay, value, nameof(ScheduleDay)))
                {
                    Model.ScheduleDay = value;
                    CalculateNextScheduledUpdate();
                    _vmService!.SaveVirtualMachineEntry(Model);
                }
            }
        }
        public DateTime ScheduleTime
        {
            get => _scheduleTime;
            set
            {
                if (SetProperty(ref _scheduleTime, value, nameof(ScheduleTime)))
                {
                    Model.ScheduleTime = value;
                    CalculateNextScheduledUpdate();
                    _vmService!.SaveVirtualMachineEntry(Model);
                }
            }
        }
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (SetProperty(ref _isExpanded, value, nameof(IsExpanded)))
                {
                    ExpandedIcon = _isExpanded ? "\uE70E" : "\uE70D";
                    if (IsExpanded) _onExpanded?.Invoke(this);
                }
            }
        }
        public string ExpandedIcon { get => _expandedIcon; private set => SetProperty(ref _expandedIcon, value, nameof(ExpandedIcon)); }
        public DateTime LastUpdate
        {
            get => Model.LastUpdate;
            set
            {
                if (SetProperty(() => Model.LastUpdate, v => Model.LastUpdate = v, value))
                {
                    OnPropertyChanged(nameof(LastUpdateDisplayText));
                }
            }
        }
        public string LastUpdateDisplayText
        {
            get
            {
                if (Model.LastUpdate == DateTime.MinValue) return "Last Update: Never";
                return "Last Update: " + Model.LastUpdate.ToString("dddd, MMMM d 'at' hh:mm tt");
            }
        }
        public string NextUpdateDisplayText
        {
            get
            {
                if (Model.NextUpdate == DateTime.MinValue) return "Next Update: Never";
                return "Next Update: " + Model.NextUpdate.ToString("dddd, MMMM d 'at' hh:mm tt");
            }
        }
        #endregion

        private void ToggleExpand() => IsExpanded = !IsExpanded;

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
