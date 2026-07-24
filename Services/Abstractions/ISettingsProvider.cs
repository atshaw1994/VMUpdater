namespace VMUpdater.Services.Abstractions
{
    public interface ISettingsProvider
    {
        string VMRunPath { get; }
        string VBoxManagePath { get; }
    }
}