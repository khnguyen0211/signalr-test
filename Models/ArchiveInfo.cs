namespace WarpBootstrap.Models
{
    public class ArchiveInfo
    {
        public string ArchiveType { get; set; } = string.Empty;
        public long CompressedSize { get; set; }
        public long UncompressedSize { get; set; }
        public int EntryCount { get; set; }
        public List<string> FileList { get; set; } = new();
        public bool IsPasswordProtected { get; set; }
    }
}
