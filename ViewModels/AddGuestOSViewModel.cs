// VMUpdater - Automated headless VM update scheduler
// Copyright (C) 2025 Aaron Shaw
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VMUpdater.Models;

namespace VMUpdater.ViewModels
{
    public partial class AddGuestOSViewModel : ObservableObject
    {
        public GuestOSModel CreatedGuestOS { get; } = new();

        #region Properties

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _networkCheckArgumentTemplate = string.Empty;

        #endregion

        #region Commands

        [RelayCommand(CanExecute = nameof(CanSave))]
        private void Save(ICloseable? window)
        {
            // Populate the new model instance
            CreatedGuestOS.Name = Name.Trim();
            CreatedGuestOS.NetworkCheckArgumentTemplate = NetworkCheckArgumentTemplate.Trim();

            // Close dialog passing true to signal successful creation
            window?.Close(true);
        }

        private bool CanSave() => !string.IsNullOrWhiteSpace(Name);

        [RelayCommand]
        private static void Cancel(ICloseable? window) => window?.Close(false);

        #endregion
    }
}