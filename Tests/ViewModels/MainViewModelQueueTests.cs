using System.Threading.Tasks;
using Xunit;
using NSubstitute;
using VMUpdater.Models;
using VMUpdater.Services;
using VMUpdater.ViewModels;

namespace VMUpdater.Tests.ViewModels
{
    public class MainViewModelQueueTests
    {
        private readonly VirtualMachineService _vmServiceMock;

        public MainViewModelQueueTests()
        {
            _vmServiceMock = Substitute.For<VirtualMachineService>();
        }

        [Fact]
        public void EnqueueUpdateRequest_IgnoresDuplicateVMs()
        {
            // Arrange
            var mainVm = new MainViewModel();
            var model = new VirtualMachineModel { Hypervisor = HypervisorType.VMWare };
            var vm = new VirtualMachineViewModel(model, _vmServiceMock, _ => { });

            // Set state to updating so queue doesn't auto-dequeue immediately during test
            mainVm.IsUpdating = true;

            // Act
            mainVm.EnqueueUpdateRequest(vm);
            mainVm.EnqueueUpdateRequest(vm); // Duplicate request

            // Assert
            // LogText output captures the rejection log entry
            Assert.Contains("Update request ignored: VM is already queued.", mainVm.LogText);
        }

        [Theory]
        [InlineData("VMWare", HypervisorType.VMWare)]
        [InlineData("VirtualBox", HypervisorType.VirtualBox)]
        [InlineData("QEMU", HypervisorType.QEMU)]
        [InlineData("InvalidHypervisor", HypervisorType.VMWare)]
        public void AddVirtualMachine_ParsesHypervisorTypeCorrectly(string hypervisorInput, HypervisorType expectedType)
        {
            // Arrange
            var mainVm = new MainViewModel();
            mainVm.VirtualMachines.Clear(); // Clear AppData entries loaded during constructor initialization

            // Act
            mainVm.AddVirtualMachineCommand.Execute(hypervisorInput);

            // Assert
            Assert.Single(mainVm.VirtualMachines);
            Assert.Equal(expectedType, mainVm.VirtualMachines[0].HypervisorType);
        }

        [Fact]
        public void IsUpdating_TriggersToolTipPropertyAndCallback()
        {
            // Arrange
            var mainVm = new MainViewModel();
            string? reportedToolTip = null;
            mainVm.OnTooltipRefreshRequested = (tooltip) => reportedToolTip = tooltip;

            // Act
            mainVm.IsUpdating = true;

            // Assert
            Assert.Equal("VMUpdater\nUpdating...", mainVm.TrayToolTipText);
            Assert.Equal("VMUpdater\nUpdating...", reportedToolTip);
        }
    }
}