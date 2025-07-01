using System.Diagnostics;
using WarpBootstrap.Models;
using WarpBootstrap.Services.Interfaces;

namespace WarpBootstrap.Services.Implementations
{
    public class LinuxScriptExecutor : IScriptExecutor
    {
        public async Task<ScriptExecutionResult> ExecuteScriptAsync(string scriptPath)
        {
            var processStartInfo = new ProcessStartInfo("/bin/bash", $"{scriptPath}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processStartInfo };
            process.Start();

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            process.WaitForExit();

            return new ScriptExecutionResult
            {
                ScriptName = Path.GetFileName(scriptPath),
                ExitCode = process.ExitCode,
                StandardOutput = stdout,
                StandardError = stderr
            };
        }
    }

}
