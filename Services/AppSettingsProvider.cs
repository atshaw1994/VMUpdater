using VMUpdater.Services.Abstractions;

namespace VMUpdater.Services
{
    public class AppSettingsProvider : ISettingsProvider
    {
        public string VMRunPath => Properties.Settings.Default.VMRunPath;
        public string VBoxManagePath => Properties.Settings.Default.VBoxManagePath;
    }
}