using WarpBootstrap.Models;

namespace WarpBootstrap.Services.Interfaces
{
    public interface IScriptExecutor
    {
        bool CanExecute(string scriptPath);
        Task<ScriptResult> ExecuteAsync(string scriptPath, string workingDirectory, CancellationToken cancellationToken = default);
        Task<ScriptResult> ExecuteWithTimeoutAsync(string scriptPath, string workingDirectory, TimeSpan timeout);
        string[] SupportedExtensions { get; }
    }
}
