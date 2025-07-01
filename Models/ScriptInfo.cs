namespace WarpBootstrap.Models
{
    public class ScriptInfo
    {
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public ScriptType Type { get; set; }
        public bool Exists { get; set; }
        public bool IsExecutable { get; set; }
        public ScriptPhase Phase { get; set; }
    }
}
public enum ScriptType
{
    Batch,
    PowerShell,
    Shell,
    Unknown
}
public enum ScriptPhase
{
    PreInstallation,
    Installation,
    PostInstallation
}