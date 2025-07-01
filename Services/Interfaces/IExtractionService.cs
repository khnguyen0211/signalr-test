using WarpBootstrap.Models;

namespace WarpBootstrap.Services.Interfaces
{
    public interface IExtractionService
    {
        Task<ExtractionResult> ExtractArchiveAsync(string archivePath, string connectionId);
        Task<ArchiveInfo> GetArchiveInfoAsync(string archivePath);
        bool IsSupportedArchive(string fileName);
        string GetExtractionPath(string connectionId, string fileName);
        Task CleanupExtractionAsync(string connectionId);
    }
}
