using System;
using System.IO;
using VMUpdater.Models;

namespace VMUpdater.Services.Orchestration
{
    public static class CommandTemplateExpander
    {
        public static string ExpandArguments(
            string template,
            VirtualMachineModel vm,
            string scriptCommand = "")
        {
            if (string.IsNullOrWhiteSpace(template))
                return string.Empty;

            string vmName = Path.GetFileNameWithoutExtension(vm.VMPath ?? string.Empty);

            // Escape double quotes inside credentials to avoid breaking CLI strings
            string safeUsername = (vm.Username ?? string.Empty).Replace("\"", "\\\"");
            string safePassword = (vm.Password ?? string.Empty).Replace("\"", "\\\"");

            return template
                .Replace(CommandTokens.VmPath, vm.VMPath ?? string.Empty)
                .Replace(CommandTokens.VmName, vmName)
                .Replace(CommandTokens.Username, safeUsername)
                .Replace(CommandTokens.Password, safePassword)
                .Replace(CommandTokens.ScriptCommand, scriptCommand);
        }
    }
}