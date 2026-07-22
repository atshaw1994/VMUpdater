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

        [Fact]
        public async Task EnqueueUpdateRequest_ProcessesItemsSequentiallyInQueue()
        {
            // Arrange: Pass the mock service into MainViewModel so it doesn't construct its own concrete service!
            var mainVm = new MainViewModel(_vmServiceMock);
            mainVm.VirtualMachines.Clear();

            var vm1 = new VirtualMachineViewModel(new VirtualMachineModel { VMPath = @"C:\VMs\VM1.vmx" }, _vmServiceMock, _ => { });
            var vm2 = new VirtualMachineViewModel(new VirtualMachineModel { VMPath = @"C:\VMs\VM2.vmx" }, _vmServiceMock, _ => { });

            var vm1UpdateTask = new TaskCompletionSource<bool>();
            var vm2UpdateTask = new TaskCompletionSource<bool>();

            int vm1ExecutionCount = 0;
            int vm2ExecutionCount = 0;

            _vmServiceMock.StartUpdateAsync(
                Arg.Any<VirtualMachineModel>(),
                Arg.Any<Action<UpdateProgressReport>>(),
                Arg.Any<Func<string, string, Task<int>>>()
            ).Returns(async call =>
            {
                var model = call.Arg<VirtualMachineModel>();

                if (model.VMPath == vm1.Model.VMPath)
                {
                    vm1ExecutionCount++;
                    await vm1UpdateTask.Task;
                }
                else if (model.VMPath == vm2.Model.VMPath)
                {
                    vm2ExecutionCount++;
                    await vm2UpdateTask.Task;
                }
            });

            // Act & Assert Step 1: Enqueue VM1
            mainVm.EnqueueUpdateRequest(vm1);
            await Task.Delay(50);

            Assert.True(mainVm.IsUpdating);
            Assert.Equal(1, vm1ExecutionCount);
            Assert.Equal(0, vm2ExecutionCount);

            // Act & Assert Step 2: Enqueue VM2 while VM1 is active
            mainVm.EnqueueUpdateRequest(vm2);
            await Task.Delay(50);

            Assert.True(mainVm.IsUpdating);
            Assert.Equal(1, vm1ExecutionCount);
            Assert.Equal(0, vm2ExecutionCount);

            // Act & Assert Step 3: Complete VM1 and verify VM2 executes automatically
            vm1UpdateTask.SetResult(true);
            await Task.Delay(100);

            Assert.Equal(1, vm1ExecutionCount);
            Assert.Equal(1, vm2ExecutionCount);

            // Cleanup VM2
            vm2UpdateTask.SetResult(true);
            await Task.Delay(50);

            Assert.False(mainVm.IsUpdating);
        }
    }
}