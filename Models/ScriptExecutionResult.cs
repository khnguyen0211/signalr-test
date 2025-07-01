namespace WarpBootstrap.Models
{
    public class ScriptExecutionResult
    {
        public string ScriptName { get; set; } = null!;
        public int ExitCode { get; set; }
        public string StandardOutput { get; set; } = string.Empty;
        public string StandardError { get; set; } = string.Empty;
        public bool IsSuccess => ExitCode == 0;
    }
}
