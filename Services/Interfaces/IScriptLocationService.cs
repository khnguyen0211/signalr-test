using WarpBootstrap.Models;

namespace WarpBootstrap.Services.Interfaces
{
    public interface IScriptLocationService
    {
        ScriptSet LocateScripts(string basePath, string version, string os);
        bool ValidateScriptPath(string scriptPath);
        List<string> GetAvailableVersions(string basePath);
        List<string> GetSupportedOS(string basePath, string version);
    }
}
