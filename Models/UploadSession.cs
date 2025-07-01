namespace WarpBootstrap.Models
{
    public class UploadSession
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public FileStream? FileStream { get; set; }
        public DateTime StartTime { get; set; }
        public long BytesReceived { get; set; }
    }

}
