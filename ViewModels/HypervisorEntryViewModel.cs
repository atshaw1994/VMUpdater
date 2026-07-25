using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace VMUpdater.ViewModels
{
    public partial class HypervisorEntryViewModel : ObservableObject
    {
        [ObservableProperty]
        public partial string HypervisorName { get; set; } = "Hypervisor";

        [ObservableProperty]
        public partial string TempName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsEditingName { get; set; }

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
