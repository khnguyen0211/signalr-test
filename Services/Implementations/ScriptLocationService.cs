using WarpBootstrap.Models;
using WarpBootstrap.Services.Interfaces;

namespace WarpBootstrap.Services.Implementations
{
    public class ScriptLocationService : IScriptLocationService
    {
        private static readonly Lazy<ScriptLocationService> _instance = new(() => new ScriptLocationService());
        public static ScriptLocationService Instance => _instance.Value;

        private readonly Dictionary<string, string> _scriptFileNames = new()
        {
            { "windows_pre", "pre-installation.bat" },
            { "windows_main", "installation.bat" },
            { "windows_post", "post-installation.bat" },
            { "linux_pre", "pre-installation.sh" },
            { "linux_main", "installation.sh" },
            { "linux_post", "post-installation.sh" }
        };

        private ScriptLocationService() { }

        public ScriptSet LocateScripts(string basePath, string version, string os)
        {
            var scriptSet = new ScriptSet();

            try
            {
                // Construct the path: basePath/version/os/
                var scriptDirectory = Path.Combine(basePath, version, os.ToLower());

                if (!Directory.Exists(scriptDirectory))
                {
                    Console.WriteLine($"[ScriptLocator] Directory not found: {scriptDirectory}");
                    return scriptSet;
                }

                // Locate each script
                var osKey = os.ToLower();

                // Pre-installation script
                var preScriptName = GetScriptFileName(osKey, "pre");
                if (!string.IsNullOrEmpty(preScriptName))
                {
                    scriptSet.PreInstallation = CreateScriptInfo(
                        Path.Combine(scriptDirectory, preScriptName),
                        preScriptName,
                        ScriptPhase.PreInstallation
                    );
                }

                // Main installation script
                var mainScriptName = GetScriptFileName(osKey, "main");
                if (!string.IsNullOrEmpty(mainScriptName))
                {
                    scriptSet.Installation = CreateScriptInfo(
                        Path.Combine(scriptDirectory, mainScriptName),
                        mainScriptName,
                        ScriptPhase.Installation
                    );
                }

                // Post-installation script
                var postScriptName = GetScriptFileName(osKey, "post");
                if (!string.IsNullOrEmpty(postScriptName))
                {
                    scriptSet.PostInstallation = CreateScriptInfo(
                        Path.Combine(scriptDirectory, postScriptName),
                        postScriptName,
                        ScriptPhase.PostInstallation
                    );
                }

                Console.WriteLine($"[ScriptLocator] Found {scriptSet.GetOrderedScripts().Count} scripts for {version}/{os}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ScriptLocator Error] {ex.Message}");
            }

            return scriptSet;
        }

        private string GetScriptFileName(string os, string phase)
        {
            var key = $"{os}_{phase}";
            return _scriptFileNames.TryGetValue(key, out var fileName) ? fileName : string.Empty;
        }

        private ScriptInfo CreateScriptInfo(string path, string name, ScriptPhase phase)
        {
            var info = new ScriptInfo
            {
                Path = path,
                Name = name,
                Phase = phase,
                Exists = File.Exists(path),
                Type = DetermineScriptType(path)
            };

            // For Windows, .bat files are always executable
            if (info.Type == ScriptType.Batch || info.Type == ScriptType.PowerShell)
            {
                info.IsExecutable = info.Exists;
            }

            return info;
        }

        private ScriptType DetermineScriptType(string path)
        {
            var extension = Path.GetExtension(path).ToLower();
            return extension switch
            {
                ".bat" => ScriptType.Batch,
                ".ps1" => ScriptType.PowerShell,
                ".sh" => ScriptType.Shell,
                _ => ScriptType.Unknown
            };
        }

        public bool ValidateScriptPath(string scriptPath)
        {
            try
            {
                // Check for path traversal attempts
                var fullPath = Path.GetFullPath(scriptPath);
                var fileName = Path.GetFileName(scriptPath);

                // Check for dangerous patterns
                if (scriptPath.Contains("..") ||
                    scriptPath.Contains("~") ||
                    Path.IsPathRooted(scriptPath) && !scriptPath.StartsWith(Path.GetTempPath()))
                {
                    return false;
                }

                // Validate file name
                var invalidChars = Path.GetInvalidFileNameChars();
                if (fileName.Any(c => invalidChars.Contains(c)))
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public List<string> GetAvailableVersions(string basePath)
        {
            var versions = new List<string>();

            try
            {
                if (Directory.Exists(basePath))
                {
                    var directories = Directory.GetDirectories(basePath);
                    foreach (var dir in directories)
                    {
                        var dirName = Path.GetFileName(dir);
                        if (dirName.StartsWith("python", StringComparison.OrdinalIgnoreCase))
                        {
                            versions.Add(dirName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ScriptLocator] Error getting versions: {ex.Message}");
            }

            return versions;
        }

        public List<string> GetSupportedOS(string basePath, string version)
        {
            var supportedOS = new List<string>();

            try
            {
                var versionPath = Path.Combine(basePath, version);
                if (Directory.Exists(versionPath))
                {
                    var directories = Directory.GetDirectories(versionPath);
                    foreach (var dir in directories)
                    {
                        var osName = Path.GetFileName(dir);
                        supportedOS.Add(osName);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ScriptLocator] Error getting supported OS: {ex.Message}");
            }

            return supportedOS;
        }
    }
}
