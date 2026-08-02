// VMUpdater - Automated headless VM update scheduler
// Copyright (C) 2025 Aaron Shaw
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        private async Task BrowseExecutableAsync()
        {
            if ((Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow is not { } mainWindow)
                return;

            var topLevel = TopLevel.GetTopLevel(mainWindow);
            if (topLevel == null) return;

            IStorageFolder? initialFolder = null;
            if (!string.IsNullOrWhiteSpace(ExecutablePath) && File.Exists(ExecutablePath))
            {
                string? directory = Path.GetDirectoryName(ExecutablePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    initialFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(directory);
                }
            }

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Hypervisor Executable",
                AllowMultiple = false,
                SuggestedStartLocation = initialFolder,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Executable Files") { Patterns = new[] { "*.exe" } },
                    FilePickerFileTypes.All
                }
            });

            var file = files.FirstOrDefault();
            if (file != null)
            {
                ExecutablePath = file.Path.LocalPath;

                // Auto-fill Name from file if Name is currently empty
                if (string.IsNullOrWhiteSpace(Name))
                {
                    Name = Path.GetFileNameWithoutExtension(ExecutablePath);
                }
            }
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private void Save(ICloseable? window)
        {
            // Populate the new model instance
            CreatedHypervisor.Name = Name.Trim();
            CreatedHypervisor.ExecutablePath = ExecutablePath.Trim();
            CreatedHypervisor.StartVMArgumentTemplate = StartVMArgument.Trim();
            CreatedHypervisor.StopVMArgumentTemplate = StopVMArgument.Trim();
            CreatedHypervisor.RunScriptArgumentTemplate = RunScriptArgument.Trim();

            // Close dialog passing true to signal successful creation
            window?.Close(true);
        }

        private bool CanSave()
        {
            return !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(ExecutablePath);
        }

        [RelayCommand]
        private static void Cancel(ICloseable? window)
        {
            window?.Close(false);
        }

        #endregion
    }
}