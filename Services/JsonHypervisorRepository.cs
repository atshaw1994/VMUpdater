using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using VMUpdater.Models;
using VMUpdater.Services.Abstractions;

namespace VMUpdater.Services
{
    public class JsonHypervisorRepository : IHypervisorRepository
    {
        private readonly string _storageFolder;
        private readonly JsonSerializerOptions _jsonOptions;

        public JsonHypervisorRepository()
        {
            _storageFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VMUpdater\\Hypervisors\\"
            );
            Directory.CreateDirectory(_storageFolder);

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        public async Task SaveAsync(HypervisorModel hypervisor)
        {
            ArgumentNullException.ThrowIfNull(hypervisor);

            string filePath = Path.Combine(_storageFolder, $"{hypervisor.Id:N}.json");
            using FileStream stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, hypervisor, _jsonOptions).ConfigureAwait(false);
        }

        public Task DeleteAsync(HypervisorModel hypervisor)
        {
            ArgumentNullException.ThrowIfNull(hypervisor);

            string filePath = Path.Combine(_storageFolder, $"{hypervisor.Id:N}.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            return Task.CompletedTask;
        }

        public async Task<IEnumerable<HypervisorModel>> LoadAllAsync()
        {
            var hypervisors = new List<HypervisorModel>();
            if (!Directory.Exists(_storageFolder)) return hypervisors;

            foreach (string filePath in Directory.GetFiles(_storageFolder, "*.json"))
            {
                try
                {
                    using FileStream stream = File.OpenRead(filePath);
                    var hypervisor = await JsonSerializer.DeserializeAsync<HypervisorModel>(stream, _jsonOptions).ConfigureAwait(false);
                    if (hypervisor != null)
                        hypervisors.Add(hypervisor);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Deserialization failed: {ex}");
                }
            }

            return hypervisors;
        }

        public async Task<HypervisorModel?> GetByIdAsync(Guid hypervisorId)
        {
            string filePath = Path.Combine(_storageFolder, $"{hypervisorId:N}.json");
            if (!File.Exists(filePath)) return null;

            try
            {
                using FileStream stream = File.OpenRead(filePath);
                var hypervisor = await JsonSerializer.DeserializeAsync<HypervisorModel>(stream, _jsonOptions).ConfigureAwait(false);
                return hypervisor!;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Deserialization failed: {ex}");
                return null!;
            }
        }
    }
}
