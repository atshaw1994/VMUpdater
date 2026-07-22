using System.Threading.Tasks;
using Xunit;
using VMUpdater.Models;
using VMUpdater.Services;

namespace VMUpdater.Tests.Services
{
    public class VirtualMachineServiceTests
    {
        [Fact]
        public async Task StartUpdateAsync_WhenPingFails_AbortsAndReturnsFalse()
        {
            // Arrange
            var service = new VirtualMachineService();
            var vm = new VirtualMachineModel
            {
                Hypervisor = HypervisorType.VirtualBox,
                VMPath = @"C:\VMs\Ubuntu.vbox",
                GuestOSType = "Ubuntu"
            };

            int progressReportCount = 0;
            string? lastStatusMessage = null;

            // Mock the process execution delegate (simulating a failed ping returning exit code 1)
            static Task<int> MockProcessRunner(string fileName, string args)
            {
                if (args.Contains("ping"))
                {
                    return Task.FromResult(1); // Non-zero exit code == failure
                }
                return Task.FromResult(0);
            }

            // Act
            await service.StartUpdateAsync(
                vm,
                report =>
                {
                    progressReportCount++;
                    if (!string.IsNullOrEmpty(report.StatusText))
                        lastStatusMessage = report.StatusText;
                },
                MockProcessRunner
            );

            // Assert
            Assert.True(progressReportCount > 0);
            Assert.Equal("Aborted: Network connectivity validation failed.", lastStatusMessage);
        }
    }
}