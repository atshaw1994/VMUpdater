using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Xml.Linq;
using VMUpdater.Models;

namespace VMUpdater.ViewModels
{
    public partial class AddGuestOSViewModel : ObservableObject
    {
        public GuestOSModel CreatedGuestOS { get; } = new();

        #region Editable Properties

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _networkCheckArgumentTemplate = string.Empty;

        #endregion

        #region Commands

        [RelayCommand(CanExecute = nameof(CanSave))]
        private void Save(Window window)
        {
            // Populate the new model instance
            CreatedGuestOS.Name = Name.Trim();
            CreatedGuestOS.NetworkCheckArgumentTemplate = NetworkCheckArgumentTemplate.Trim();

            // Set DialogResult to true to signal successful creation
            if (window != null)
            {
                window.DialogResult = true;
                window.Close();
            }
        }

        private bool CanSave() => !string.IsNullOrWhiteSpace(Name);

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
