using System.Diagnostics;
using System.Text;
using WarpBootstrap.Models;
using WarpBootstrap.Services.Interfaces;

namespace WarpBootstrap.Services.Implementations
{
    public class WindowsScriptExecutor : IScriptExecutor
    {
        public string[] SupportedExtensions => new[] { ".bat", ".cmd", ".ps1" };

        private readonly int _defaultTimeoutSeconds = 3600; // 1 hour

        public bool CanExecute(string scriptPath)
        {
            if (string.IsNullOrWhiteSpace(scriptPath))
                return false;

            var extension = Path.GetExtension(scriptPath).ToLower();
            return SupportedExtensions.Contains(extension) && File.Exists(scriptPath);
        }

        public async Task<ScriptResult> ExecuteAsync(string scriptPath, string workingDirectory, CancellationToken cancellationToken = default)
        {
            var timeout = TimeSpan.FromSeconds(_defaultTimeoutSeconds);
            return await ExecuteWithTimeoutAsync(scriptPath, workingDirectory, timeout);
        }

        public async Task<ScriptResult> ExecuteWithTimeoutAsync(string scriptPath, string workingDirectory, TimeSpan timeout)
        {
            var result = new ScriptResult
            {
                ScriptName = Path.GetFileName(scriptPath),
                StartTime = DateTime.UtcNow
            };

            try
            {
                if (!CanExecute(scriptPath))
                {
                    throw new InvalidOperationException($"Cannot execute script: {scriptPath}");
                }

                var extension = Path.GetExtension(scriptPath).ToLower();
                ProcessStartInfo startInfo;

                if (extension == ".ps1")
                {
                    // PowerShell script
                    startInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                        WorkingDirectory = workingDirectory,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };
                }
                else
                {
                    // Batch file (.bat, .cmd)
                    startInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c \"{scriptPath}\"",
                        WorkingDirectory = workingDirectory,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };
                }

                // Add environment variables if needed
                startInfo.EnvironmentVariables["SCRIPT_DIR"] = Path.GetDirectoryName(scriptPath);
                startInfo.EnvironmentVariables["WORKING_DIR"] = workingDirectory;

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                using var process = new Process { StartInfo = startInfo };

                // Event handlers for output
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        outputBuilder.AppendLine(e.Data);
                        Console.WriteLine($"[Script Output] {e.Data}");
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        errorBuilder.AppendLine(e.Data);
                        Console.WriteLine($"[Script Error] {e.Data}");
                    }
                };

                Console.WriteLine($"[Executor] Starting script: {scriptPath}");
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Wait for exit with timeout
                using var cts = new CancellationTokenSource(timeout);
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Timeout occurred
                    Console.WriteLine($"[Executor] Script timed out after {timeout.TotalSeconds} seconds");

                    try
                    {
                        process.Kill(true); // Kill entire process tree
                    }
                    catch (Exception killEx)
                    {
                        Console.WriteLine($"[Executor] Failed to kill process: {killEx.Message}");
                    }

                    result.ExitCode = -1;
                    result.ErrorOutput = $"Script execution timed out after {timeout.TotalSeconds} seconds";
                    result.EndTime = DateTime.UtcNow;
                    result.Duration = result.EndTime - result.StartTime;
                    return result;
                }

                result.ExitCode = process.ExitCode;
                result.Output = outputBuilder.ToString();
                result.ErrorOutput = errorBuilder.ToString();

                Console.WriteLine($"[Executor] Script completed with exit code: {result.ExitCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Executor Error] {ex.Message}");
                result.ExitCode = -999;
                result.ErrorOutput = $"Exception: {ex.Message}";
            }

            result.EndTime = DateTime.UtcNow;
            result.Duration = result.EndTime - result.StartTime;

            return result;
        }
    }
}
