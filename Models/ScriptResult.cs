namespace WarpBootstrap.Models
{
    public class ScriptResult
    {
        public string ScriptName { get; set; } = string.Empty;
        public ScriptPhase Phase { get; set; }
        public int ExitCode { get; set; }
        public string Output { get; set; } = string.Empty;
        public string ErrorOutput { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public bool Success => ExitCode == 0;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
}
