using System;
using Xunit;
using NSubstitute;
using VMUpdater.Models;
using VMUpdater.Services;
using VMUpdater.ViewModels;

namespace VMUpdater.Tests.ViewModels
{
    public class VirtualMachineViewModelTests
    {
        private readonly VirtualMachineService _vmServiceMock;

        public VirtualMachineViewModelTests()
        {
            // Create a fake service double (similar to RSpec double/allow)
            _vmServiceMock = Substitute.For<VirtualMachineService>();
        }

        [Fact]
        public void ToggleExpand_TogglesIsExpandedAndIcon()
        {
            // Arrange
            bool expandedCallbackFired = false;
            var model = new VirtualMachineModel();
            var vm = new VirtualMachineViewModel(model, _vmServiceMock, onExpanded: _ => expandedCallbackFired = true);

            // Act
            vm.ToggleExpandCommand.Execute(null);

            // Assert
            Assert.True(vm.IsExpanded);
            Assert.Equal("\uE70E", vm.ExpandedIcon);
            Assert.True(expandedCallbackFired);

            // Act again
            vm.ToggleExpandCommand.Execute(null);

            // Assert
            Assert.False(vm.IsExpanded);
            Assert.Equal("\uE70D", vm.ExpandedIcon);
        }

        [Theory]
        [InlineData("Monday", "2026-07-20 14:00:00", "Monday")]
        [InlineData("Friday", "2026-07-20 14:00:00", "Friday")]
        public void CalculateNextScheduledUpdate_CalculatesCorrectUpcomingDate(string targetDay, string targetTimeString, string expectedDayInDisplayText)
        {
            // Arrange
            var targetTime = DateTime.Parse(targetTimeString);
            var model = new VirtualMachineModel
            {
                ScheduleDay = targetDay,
                ScheduleTime = targetTime
            };

            var vm = new VirtualMachineViewModel(model, _vmServiceMock, _ => { });

            // Act
            vm.CalculateNextScheduledUpdate();

            // Assert
            Assert.NotEqual(DateTime.MinValue, vm.Model.NextUpdate);
            Assert.Contains(expectedDayInDisplayText, vm.NextUpdateDisplayText);
        }

        [Fact]
        public void LastUpdateDisplayText_WhenNeverUpdated_ReturnsNever()
        {
            // Arrange
            var model = new VirtualMachineModel { LastUpdate = DateTime.MinValue };
            var vm = new VirtualMachineViewModel(model, _vmServiceMock, _ => { });

            // Assert
            Assert.Equal("Last Update: Never", vm.LastUpdateDisplayText);
        }

        [Fact]
        public void VMPath_WhenChanged_UpdatesDisplayNameAndSavesEntry()
        {
            // Arrange
            var model = new VirtualMachineModel();
            var vm = new VirtualMachineViewModel(model, _vmServiceMock, _ => { })
            {
                // Act
                VMPath = @"C:\VirtualMachines\ArchLinuxTest.vmx"
            };

            // Assert
            Assert.Equal("ArchLinuxTest", vm.DisplayName);
            _vmServiceMock.Received(1).SaveVirtualMachineEntry(model);
        }
    }
}