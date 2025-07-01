using WarpBootstrap.Models;

namespace WarpBootstrap.Services.Interfaces
{
    public interface IInstallerService
    {
        Task<List<ScriptExecutionResult>> InstallAsync(InstallRequest request);
    }
}
