using WarpBootstrap.Models;
using WarpBootstrap.Services.Interfaces;
using WarpBootstrap.Utilities;

namespace WarpBootstrap.Services.Implementations
{
    public class InstallerService : IInstallerService
    {
        private readonly ZipExtractor _zipExtractor;

        public InstallerService(ZipExtractor zipExtractor)
        {
            _zipExtractor = zipExtractor;
        }

        public async Task<List<ScriptExecutionResult>> InstallAsync(InstallRequest request)
        {
            var extractionPath = Path.Combine(Path.GetTempPath(),
                $"/Warp/Extract/{Guid.NewGuid()}");

            _zipExtractor.Extract(request.ZipFilePath, extractionPath);

            string scriptExtension;
            IScriptExecutor executor;

            if (request.OS.Equals("windows", StringComparison.OrdinalIgnoreCase))
            {
                scriptExtension = ".bat";
                executor = new WindowsScriptExecutor();
            }
            else if (request.OS.Equals("linux", StringComparison.OrdinalIgnoreCase))
            {
                scriptExtension = ".sh";
                executor = new LinuxScriptExecutor();
            }
            else
            {
                throw new ArgumentException($"Unsupported OS: {request.OS}");
            }

            var scriptsToRun = new[]
            {
                "pre-installation" + scriptExtension,
                "installation" + scriptExtension,
                "post-installation" + scriptExtension
            };

            var results = new List<ScriptExecutionResult>();

            foreach (var scriptName in scriptsToRun)
            {
                var scriptPath = Path.Combine(
                    extractionPath,
                    request.Version,
                    request.OS.Equals("windows", StringComparison.OrdinalIgnoreCase) ? "windows" : "ubuntu",
                    scriptName
                );

                if (!File.Exists(scriptPath))
                    throw new FileNotFoundException($"Script not found: {scriptPath}");

                var result = await executor.ExecuteScriptAsync(scriptPath);
                results.Add(result);

                if (!result.IsSuccess)
                {
                    break;
                }
            }

            return results;
        }
    }
}
