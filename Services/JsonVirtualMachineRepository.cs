using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using VMUpdater.Models;
using VMUpdater.Services.Abstractions;

namespace VMUpdater.Services
{
    public class JsonVirtualMachineRepository : IVirtualMachineRepository
    {
        private readonly string _storageFolder;
        private readonly JsonSerializerOptions _jsonOptions;

        public JsonVirtualMachineRepository()
        {
            _storageFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VMUpdater\\VirtualMachines\\"
            );
            Directory.CreateDirectory(_storageFolder);

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        public async Task SaveAsync(VirtualMachineModel vm)
        {
            ArgumentNullException.ThrowIfNull(vm);

            string filePath = Path.Combine(_storageFolder, $"{vm.Id:N}.json");
            using FileStream stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, vm, _jsonOptions);
        }

        public Task DeleteAsync(VirtualMachineModel vm)
        {
            ArgumentNullException.ThrowIfNull(vm);

            string filePath = Path.Combine(_storageFolder, $"{vm.Id:N}.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            return Task.CompletedTask;
        }

        public async Task<IEnumerable<VirtualMachineModel>> LoadAllAsync()
        {
            var vms = new List<VirtualMachineModel>();
            if (!Directory.Exists(_storageFolder)) return vms;

            foreach (string filePath in Directory.GetFiles(_storageFolder, "*.json"))
            {
                try
                {
                    using FileStream stream = File.OpenRead(filePath);
                    var vm = await JsonSerializer.DeserializeAsync<VirtualMachineModel>(stream, _jsonOptions);
                    if (vm != null)
                    {
                        vms.Add(vm);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Deserialization failed: {ex}");
                }
            }

            return vms;
        }
    }
}