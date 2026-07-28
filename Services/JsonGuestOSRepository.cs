using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using VMUpdater.Models;
using VMUpdater.Services.Abstractions;
using VMUpdater.Services.Orchestration;

namespace VMUpdater.Services
{
    public class JsonGuestOSRepository : IGuestOSRepository
    {
        private readonly string _storageFolder;
        private readonly JsonSerializerOptions _jsonOptions;

        public JsonGuestOSRepository()
        {
            _storageFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VMUpdater\\GuestOS\\"
            );
            Directory.CreateDirectory(_storageFolder);

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        public async Task SaveAsync(GuestOSModel guestOS)
        {
            ArgumentNullException.ThrowIfNull(guestOS);

            string filePath = Path.Combine(_storageFolder, $"{guestOS.Id:N}.json");
            using FileStream stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, guestOS, _jsonOptions).ConfigureAwait(false);
        }

        public Task DeleteAsync(GuestOSModel guestOS)
        {
            ArgumentNullException.ThrowIfNull(guestOS);

            string filePath = Path.Combine(_storageFolder, $"{guestOS.Id:N}.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            return Task.CompletedTask;
        }

        public async Task<IEnumerable<GuestOSModel>> LoadAllAsync()
        {
            var guestOSList = new List<GuestOSModel>();
            if (!Directory.Exists(_storageFolder)) return guestOSList;

            foreach (string filePath in Directory.GetFiles(_storageFolder, "*.json"))
            {
                try
                {
                    using FileStream stream = File.OpenRead(filePath);
                    var guestOS = await JsonSerializer.DeserializeAsync<GuestOSModel>(stream, _jsonOptions).ConfigureAwait(false);
                    if (guestOS != null)
                        guestOSList.Add(guestOS);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Deserialization failed: {ex}");
                }
            }

            return guestOSList;
        }

        public async Task<GuestOSModel?> GetByIdAsync(Guid guestOSId)
        {
            string filePath = Path.Combine(_storageFolder, $"{guestOSId:N}.json");
            if (!File.Exists(filePath))
            {
                if (DefaultGuestOSTypes.IsDefaultGuestOS(guestOSId))
                    return DefaultGuestOSTypes.GetModelById(guestOSId);
                return null;
            }

            try
            {
                using FileStream stream = File.OpenRead(filePath);
                var guestOS = await JsonSerializer.DeserializeAsync<GuestOSModel>(stream, _jsonOptions).ConfigureAwait(false);
                return guestOS!;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Deserialization failed: {ex}");
                return null!;
            }
        }
    }
}
