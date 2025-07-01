namespace WarpBootstrap.Models
{
    public class ExtractionResult
    {
        public bool Success { get; set; }
        public string ExtractedPath { get; set; } = string.Empty;
        public IList<string> ExtractedFiles { get; set; } = new List<string>();
        public string ErrorMessage { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
    }
}
