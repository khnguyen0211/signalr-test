namespace WarpBootstrap.Models
{
    public class ExecutionResult
    {
        public string ExecutionId { get; set; } = Guid.NewGuid().ToString();
        public bool Success { get; set; }
        public List<ScriptResult> Results { get; set; } = new();
        public TimeSpan TotalDuration { get; set; }
        public string? FailedAtScript { get; set; }
        public string Version { get; set; } = string.Empty;
        public string OS { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public ExecutionStatus Status { get; set; } = ExecutionStatus.NotStarted;
    }

    public enum ExecutionStatus
    {
        NotStarted,
        Running,
        Completed,
        Failed,
        Cancelled
    }
}

