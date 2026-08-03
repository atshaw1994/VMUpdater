using CommunityToolkit.Mvvm.ComponentModel;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;

namespace VMUpdater.ViewModels
{
    public partial class AboutDialogViewModel : ObservableObject
    {
        private const string GitHubApiUrl = "https://api.github.com/repos/atshaw1994/VMUpdater/releases";

        [ObservableProperty]
        public partial bool IsUpdateAvailable { get; set; } = false;

        [ObservableProperty]
        public partial string CurrentVersion { get; set; } = "Unknown";

        [ObservableProperty]
        public partial string LatestVersion { get; set; } = "Checking...";

        public AboutDialogViewModel()
        {
            CurrentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(2) ?? "Unknown";
            Trace.WriteLine($"Current Version: {CurrentVersion}");

            _ = CheckForUpdatesAsync();
        }

        private async Task CheckForUpdatesAsync()
        {
            LatestVersion = await GetLatestVersionAsync();

            if (LatestVersion != "Unknown")
            {
                string cleanLatest = LatestVersion.Split('-')[0];
                IsUpdateAvailable = cleanLatest != CurrentVersion;
            }
            else
            {
                IsUpdateAvailable = false;
            }
        }

        public async Task<string> GetLatestVersionAsync()
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "VMUpdater-App");

            try
            {
                var releases = await client.GetFromJsonAsync<GitHubRelease[]>(GitHubApiUrl);
                var latestRelease = releases?.FirstOrDefault();

                if (latestRelease != null && !string.IsNullOrWhiteSpace(latestRelease.TagName))
                {
                    return latestRelease.TagName.TrimStart('v', 'V');
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching latest version: {ex.Message}");
            }

            return "Unknown";
        }

        public record GitHubRelease(
            [property: System.Text.Json.Serialization.JsonPropertyName("tag_name")] string TagName
        );

        public record GitHubAsset(
            [property: System.Text.Json.Serialization.JsonPropertyName("name")] string Name,
            [property: System.Text.Json.Serialization.JsonPropertyName("browser_download_url")] string BrowserDownloadUrl
        );
    }
}
