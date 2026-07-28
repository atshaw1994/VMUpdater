using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using VMUpdater.Models;

namespace VMUpdater.ViewModels
{
    public partial class AddHypervisorViewModel : ObservableObject
    {
        public HypervisorModel CreatedHypervisor { get; } = new();

        #region Editable Properties

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private string _name = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private string _executablePath = string.Empty;

        [ObservableProperty]
        private string _startVMArgument = string.Empty;

        [ObservableProperty]
        private string _stopVMArgument = string.Empty;

        [ObservableProperty]
        private string _runScriptArgument = string.Empty;

        #endregion

        #region Commands

        [RelayCommand]
        private void BrowseExecutable()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
                Title = "Select Hypervisor Executable"
            };

            if (!string.IsNullOrWhiteSpace(ExecutablePath) && File.Exists(ExecutablePath))
            {
                dialog.InitialDirectory = Path.GetDirectoryName(ExecutablePath);
            }

            if (dialog.ShowDialog() == true)
            {
                ExecutablePath = dialog.FileName;

                // Auto-fill Name from file if Name is currently empty
                if (string.IsNullOrWhiteSpace(Name))
                {
                    Name = Path.GetFileNameWithoutExtension(dialog.FileName);
                }
            }
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private void Save(Window window)
        {
            // Populate the new model instance
            CreatedHypervisor.Name = Name.Trim();
            CreatedHypervisor.ExecutablePath = ExecutablePath.Trim();
            CreatedHypervisor.StartVMArgumentTemplate = StartVMArgument.Trim();
            CreatedHypervisor.StopVMArgumentTemplate = StopVMArgument.Trim();
            CreatedHypervisor.RunScriptArgumentTemplate = RunScriptArgument.Trim();

            // Set DialogResult to true to signal successful creation
            if (window != null)
            {
                window.DialogResult = true;
                window.Close();
            }
        }

        private bool CanSave()
        {
            return !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(ExecutablePath);
        }

        [RelayCommand]
        private static void Cancel(Window window)
        {
            if (window != null)
            {
                window.DialogResult = false;
                window.Close();
            }
        }

        #endregion
    }
}