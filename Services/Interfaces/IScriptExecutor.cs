using WarpBootstrap.Models;

namespace WarpBootstrap.Services.Interfaces
{
    public interface IScriptExecutor
    {
        Task<ScriptExecutionResult> ExecuteScriptAsync(string scriptPath);
    }
}
