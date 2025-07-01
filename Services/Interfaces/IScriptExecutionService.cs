using WarpBootstrap.Models;

namespace WarpBootstrap.Services.Interfaces
{
    public interface IScriptExecutionService
    {
        Task<ExecutionResult> ExecuteInstallationAsync(string version, string os, string extractedPath, string connectionId);
        ExecutionStatus GetExecutionStatus(string executionId);
        bool CancelExecutionAsync(string executionId);
        Dictionary<string, ExecutionResult> GetActiveExecutions();
    }
}
