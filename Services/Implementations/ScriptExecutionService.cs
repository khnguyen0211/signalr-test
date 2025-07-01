using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using WarpBootstrap.Hubs;
using WarpBootstrap.Models;
using WarpBootstrap.Services.Interfaces;

namespace WarpBootstrap.Services.Implementations
{
    public class ScriptExecutionService : IScriptExecutionService
    {
        private static readonly Lazy<ScriptExecutionService> _instance = new(() => new ScriptExecutionService());
        public static ScriptExecutionService Instance => _instance.Value;

        private readonly IScriptLocationService _locatorService;
        private readonly ConcurrentDictionary<string, ExecutionResult> _activeExecutions = new();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens = new();
        private IHubContext<BootstrapHub>? _hubContext;

        private ScriptExecutionService()
        {
            _locatorService = ScriptLocationService.Instance;
        }

        public void SetHubContext(IHubContext<BootstrapHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task<ExecutionResult> ExecuteInstallationAsync(string version, string os, string extractedPath, string connectionId)
        {
            var executionResult = new ExecutionResult
            {
                Version = version,
                OS = os,
                StartTime = DateTime.UtcNow,
                Status = ExecutionStatus.Running
            };

            _activeExecutions[executionResult.ExecutionId] = executionResult;
            var cts = new CancellationTokenSource();
            _cancellationTokens[executionResult.ExecutionId] = cts;

            try
            {
                // Validate input
                if (string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(os))
                {
                    throw new ArgumentException("Version and OS must be provided");
                }

                if (!Directory.Exists(extractedPath))
                {
                    throw new DirectoryNotFoundException($"Extracted path not found: {extractedPath}");
                }

                // Send initial status
                await NotifyClient(connectionId, "ExecutionStarted", new { executionId = executionResult.ExecutionId, version, os });

                // Locate scripts
                var scripts = _locatorService.LocateScripts(extractedPath, version, os);
                if (!scripts.IsValid)
                {
                    throw new InvalidOperationException($"No valid installation scripts found for {version}/{os}");
                }

                // Get appropriate executor (for now, only Windows)
                IScriptExecutor executor;
                if (os.Equals("windows", StringComparison.OrdinalIgnoreCase))
                {
                    executor = new WindowsScriptExecutor();
                }
                else
                {
                    throw new NotSupportedException($"OS '{os}' is not yet supported");
                }

                // Execute scripts in order
                var orderedScripts = scripts.GetOrderedScripts();
                Console.WriteLine($"[ScriptExecution] Executing {orderedScripts.Count} scripts for {version}/{os}");

                foreach (var script in orderedScripts)
                {
                    if (cts.Token.IsCancellationRequested)
                    {
                        executionResult.Status = ExecutionStatus.Cancelled;
                        break;
                    }

                    // Notify script start
                    await NotifyClient(connectionId, "ScriptStarted", new
                    {
                        executionId = executionResult.ExecutionId,
                        scriptName = script.Name,
                        phase = script.Phase.ToString()
                    });

                    // Execute script
                    var scriptResult = await executor.ExecuteAsync(script.Path, extractedPath, cts.Token);
                    scriptResult.Phase = script.Phase;
                    executionResult.Results.Add(scriptResult);

                    // Send script output to client
                    if (!string.IsNullOrWhiteSpace(scriptResult.Output))
                    {
                        await NotifyClient(connectionId, "ScriptOutput", new
                        {
                            executionId = executionResult.ExecutionId,
                            scriptName = script.Name,
                            output = scriptResult.Output,
                            isError = false
                        });
                    }

                    if (!string.IsNullOrWhiteSpace(scriptResult.ErrorOutput))
                    {
                        await NotifyClient(connectionId, "ScriptOutput", new
                        {
                            executionId = executionResult.ExecutionId,
                            scriptName = script.Name,
                            output = scriptResult.ErrorOutput,
                            isError = true
                        });
                    }

                    // Notify script completion
                    await NotifyClient(connectionId, "ScriptCompleted", new
                    {
                        executionId = executionResult.ExecutionId,
                        scriptName = script.Name,
                        phase = script.Phase.ToString(),
                        success = scriptResult.Success,
                        exitCode = scriptResult.ExitCode,
                        duration = scriptResult.Duration.TotalSeconds
                    });

                    // Check if we should continue
                    if (!scriptResult.Success)
                    {
                        executionResult.FailedAtScript = script.Name;
                        Console.WriteLine($"[ScriptExecution] Script failed: {script.Name} (Exit code: {scriptResult.ExitCode})");

                        // For critical scripts (installation), stop execution
                        if (script.Phase == ScriptPhase.Installation)
                        {
                            break;
                        }
                    }
                }

                // Set final status
                executionResult.Success = executionResult.Results.All(r => r.Success);
                executionResult.Status = executionResult.Success ? ExecutionStatus.Completed : ExecutionStatus.Failed;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ScriptExecution Error] {ex.Message}");
                executionResult.Success = false;
                executionResult.Status = ExecutionStatus.Failed;
                executionResult.FailedAtScript = "Initialization";

                await NotifyClient(connectionId, "ExecutionError", new
                {
                    executionId = executionResult.ExecutionId,
                    error = ex.Message
                });
            }
            finally
            {
                executionResult.EndTime = DateTime.UtcNow;
                executionResult.TotalDuration = executionResult.EndTime - executionResult.StartTime;

                // Clean up cancellation token
                _cancellationTokens.TryRemove(executionResult.ExecutionId, out _);

                // Notify completion
                await NotifyClient(connectionId, "ExecutionCompleted", new
                {
                    executionId = executionResult.ExecutionId,
                    success = executionResult.Success,
                    status = executionResult.Status.ToString(),
                    totalDuration = executionResult.TotalDuration.TotalSeconds,
                    failedAtScript = executionResult.FailedAtScript
                });
            }

            return executionResult;
        }

        public ExecutionStatus GetExecutionStatus(string executionId)
        {
            return _activeExecutions.TryGetValue(executionId, out var execution)
                ? execution.Status
                : ExecutionStatus.NotStarted;
        }

        public bool CancelExecutionAsync(string executionId)
        {
            if (_cancellationTokens.TryRemove(executionId, out var cts))
            {
                cts.Cancel();

                if (_activeExecutions.TryGetValue(executionId, out var execution))
                {
                    execution.Status = ExecutionStatus.Cancelled;
                }

                Console.WriteLine($"[ScriptExecution] Cancelled execution: {executionId}");
                return true;
            }

            return false;
        }

        public Dictionary<string, ExecutionResult> GetActiveExecutions()
        {
            // Return only running executions
            return _activeExecutions
                .Where(kvp => kvp.Value.Status == ExecutionStatus.Running)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        private async Task NotifyClient(string connectionId, string method, object data)
        {
            if (_hubContext != null)
            {
                try
                {
                    await _hubContext.Clients.Client(connectionId).SendAsync(method, data);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ScriptExecution] Failed to notify client: {ex.Message}");
                }
            }
        }
    }
}
