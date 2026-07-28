// VMUpdater - Automated headless VM update scheduler
// Copyright (C) 2025  Aaron Shaw
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System.Diagnostics;
using System.IO;
using VMUpdater.Models;
using VMUpdater.Services.Abstractions;
using VMUpdater.Services.Orchestration.VMUpdater.Services.Orchestration;

namespace VMUpdater.Services.Orchestration
{
    public class UpdateProgressReport
    {
        public int ProgressDelta { get; set; }
        public string? StatusText { get; set; }
        public string? LogText { get; set; }
    }

    public class GenericHypervisorUpdater
    {
        public record VMUpdateContext(
            HypervisorModel Hypervisor,
            GuestOSModel GuestOS,
            VirtualMachineModel VM,
            Action<UpdateProgressReport> ReportProgress,
            Func<string, string, string, Task<int>> RunProcessAsync
        );

        public async Task<bool> UpdateVMAsync(VMUpdateContext ctx, string scriptCommand)
        {
            string vmIdentifier = Path.GetFileNameWithoutExtension(ctx.VM.VMPath);

            // 1. Boot VM
            _ = await StartVMAndAwaitReadyAsync(ctx);

            // 2. Network / Readiness Check
            if (!string.IsNullOrWhiteSpace(ctx.GuestOS.NetworkCheckArgumentTemplate))
            {
                _ = await CheckNetworkReadinessAsync(ctx);
            }
            else
            {
                ctx.ReportProgress(new UpdateProgressReport
                {
                    LogText = $"Failed: No network check argument template provided for guest OS '{ctx.GuestOS.Name}'!",
                    StatusText = "Update failed."
                });
                await Task.Delay(2000); // Delay to allow user to read the message
                return false;
            }

            // 3. Execute Guest Script
            ctx.ReportProgress(new UpdateProgressReport
            {
                ProgressDelta = 75,
                StatusText = "Running guest updates..."
            });

            string runScriptArgs = CommandTemplateExpander.ExpandArguments(ctx.Hypervisor.RunScriptArgumentTemplate, ctx.VM, scriptCommand);

            int scriptCode = await ctx.RunProcessAsync(ctx.VM.VMPath, ctx.Hypervisor.ExecutablePath, runScriptArgs);

            // 4. Graceful Shutdown
            ctx.ReportProgress(new UpdateProgressReport
            {
                ProgressDelta = 90,
                StatusText = "Stopping VM..."
            });

            string stopArgs = CommandTemplateExpander.ExpandArguments(ctx.Hypervisor.StopVMArgumentTemplate, ctx.VM);

            await ctx.RunProcessAsync(ctx.VM.VMPath, ctx.Hypervisor.ExecutablePath, stopArgs);

            return scriptCode == 0;
        }

        private static async Task<bool> StartVMAndAwaitReadyAsync(VMUpdateContext ctx)
        {
            // 1. Boot VM
            ctx.ReportProgress(new UpdateProgressReport
            {
                ProgressDelta = 10,
                LogText = $"Starting VM '{ctx.VM.VMPath}' via {ctx.Hypervisor.Name}...",
                StatusText = $"Starting VM via {ctx.Hypervisor.Name}..."
            });

            string startArgs = CommandTemplateExpander.ExpandArguments(ctx.Hypervisor.StartVMArgumentTemplate, ctx.VM);

            int startCode = await ctx.RunProcessAsync(ctx.VM.VMPath, ctx.Hypervisor.ExecutablePath, startArgs);
            if (startCode != 0) return false;

            ctx.ReportProgress(new UpdateProgressReport
            {
                ProgressDelta = 10,
                LogText = $"Waiting for VM '{ctx.VM.VMPath}' to boot...",
                StatusText = $"Waiting for VM '{ctx.VM.VMPath}' to boot..."
            });

            // 1.5 Wait for VM to boot/reach ready state
            bool isReady = await WaitForGuestReadyAsync(ctx);

            if (!isReady)
            {
                ctx.ReportProgress(new UpdateProgressReport
                {
                    LogText = $"Error: VM '{ctx.VM.VMPath}' failed to reach desktop within timeout."
                });
                return false;
            }

            return true;
        }

        private static async Task<bool> WaitForGuestReadyAsync(VMUpdateContext ctx)
        {
            var stopwatch = Stopwatch.StartNew();

            // Use a lightweight guest check command (e.g., VBoxManage guestcontrol check status,
            // or executing a simple 'echo ready' command inside the guest via hypervisor tools)
            string checkCommand = "echo desktop_ready";
            string readyArgs = CommandTemplateExpander.ExpandArguments(ctx.Hypervisor.RunScriptArgumentTemplate, ctx.VM, checkCommand);

            while (stopwatch.Elapsed < TimeSpan.FromMinutes(3))
            {
                int exitCode = await ctx.RunProcessAsync(ctx.VM.VMPath, ctx.Hypervisor.ExecutablePath, readyArgs);

                if (exitCode == 0)
                {
                    return true; // Guest agent responded and executed command successfully
                }

                await Task.Delay(TimeSpan.FromSeconds(5));
            }

            return false; // Timed out waiting for desktop/agent readiness
        }

        private static async Task<bool> CheckNetworkReadinessAsync(VMUpdateContext ctx)
        {
            const int maxRetries = 10;
            const int retryDelayMs = 3000;
            int netCode = -1;
            int retryCount = 0;

            ctx.ReportProgress(new UpdateProgressReport
            {
                ProgressDelta = 40,
                StatusText = "Checking guest readiness...",
                LogText = "Waiting for guest network to respond..."
            });

            string networkCommand = GuestOSProvider.GetNetworkCheckCommand(ctx.GuestOS.Name);
            string netArgs = CommandTemplateExpander.ExpandArguments(ctx.Hypervisor.RunScriptArgumentTemplate, ctx.VM, networkCommand);

            while (retryCount < maxRetries)
            {
                netCode = await ctx.RunProcessAsync(ctx.VM.VMPath, ctx.Hypervisor.ExecutablePath, netArgs);

                if (netCode == 0)
                {
                    break; // Network check succeeded
                }

                retryCount++;

                if (retryCount < maxRetries)
                {
                    ctx.ReportProgress(new UpdateProgressReport
                    {
                        ProgressDelta = 40 + (retryCount * 2),
                        StatusText = $"Guest network not ready (Attempt {retryCount}/{maxRetries})...",
                        LogText = $"Guest network check failed with exit code {netCode}. Retrying in {retryDelayMs / 1000}s..."
                    });

                    await Task.Delay(retryDelayMs);
                }
            }

            if (netCode != 0)
            {
                ctx.ReportProgress(new UpdateProgressReport
                {
                    LogText = $"Error: Guest network check failed after {maxRetries} attempts.",
                    StatusText = "Network readiness check failed."
                });

                return false; // Network check failed
            }

            ctx.ReportProgress(new UpdateProgressReport
            {
                ProgressDelta = 60,
                StatusText = "VM network is ready!",
                LogText = "Guest network confirmed active."
            });

            return true; // Network check succeeded
        }
    }
}