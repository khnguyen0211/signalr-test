namespace WarpBootstrap.Models
{
    public class InstallRequest
    {
        public string ZipFilePath { get; set; } = null!;
        public string Version { get; set; } = null!;
        public string OS { get; set; } = null!;
    }
}
