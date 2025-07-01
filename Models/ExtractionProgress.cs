namespace WarpBootstrap.Models
{
    public class ExtractionProgress
    {
        public string CurrentFile { get; set; } = string.Empty;
        public int ProcessedEntries { get; set; }
        public int TotalEntries { get; set; }
        public long ProcessedBytes { get; set; }
        public long TotalBytes { get; set; }
        public double PercentComplete => TotalEntries > 0 ? (ProcessedEntries * 100.0 / TotalEntries) : 0;
    }
}
