using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VMUpdater.Models;
using VMUpdater.Services.Abstractions;

namespace VMUpdater.ViewModels
{
    public partial class HypervisorEntryViewModel : ObservableObject
    {
        public HypervisorModel Model { get; } = new HypervisorModel();
        private readonly IHypervisorRepository? _repository;

        public HypervisorEntryViewModel(HypervisorModel model, IHypervisorRepository? repository = null)
        {
            Model = model;
            _repository = repository;

            HypervisorName = Model.Name;
            HypervisorExecutablePath = Model.ExecutablePath;
        }

        [ObservableProperty]
        public partial string HypervisorName { get; set; } = "Hypervisor";
        partial void OnHypervisorNameChanged(string value)
        {
            Model.Name = value;
            _ = SaveAsync();
        }

        [ObservableProperty]
        public partial string TempName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsEditingName { get; set; }

        [ObservableProperty]
        public partial string HypervisorExecutablePath { get; set; } = string.Empty;
        partial void OnHypervisorExecutablePathChanged(string value)
        {
            Model.ExecutablePath = value;
            _ = SaveAsync();
        }

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
            Microsoft.Win32.OpenFileDialog dialog = new();
            if (dialog.ShowDialog() == true)
            {
                HypervisorExecutablePath = dialog.FileName;
            }
        }

        private async Task SaveAsync()
        {
            if (_repository != null)
            {
                await _repository.SaveAsync(Model);
            }
        }
    }
}
