using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VMUpdater.ViewModels
{
    public partial class HypervisorEntryViewModel : ObservableObject
    {
        public HypervisorEntryViewModel() { }

        public HypervisorEntryViewModel(string hypervisorName, string executablePath)
        {
            HypervisorName = hypervisorName;
            HypervisorExecutablePath = executablePath;
        }

        [ObservableProperty]
        public partial string HypervisorName { get; set; } = "Hypervisor";

        [ObservableProperty]
        public partial string TempName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsEditingName { get; set; }

        [ObservableProperty]
        public partial string HypervisorExecutablePath { get; set; } = string.Empty;

        [RelayCommand]
        private void EditName()
        {
            TempName = HypervisorName;
            IsEditingName = true;
        }

        [RelayCommand]
        private void SaveName()
        {
            HypervisorName = TempName;
            IsEditingName = false;
        }

        [RelayCommand]
        private void CancelEdit()
        {
            IsEditingName = false;
        }

        [RelayCommand]
        public void Browse()
        {

        }
    }
}
