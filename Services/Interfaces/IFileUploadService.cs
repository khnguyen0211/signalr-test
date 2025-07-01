using WarpBootstrap.Models;

namespace WarpBootstrap.Services.Interfaces
{
    public interface IFileUploadService
    {
        string StartUploadAsync(string connectionId, UploadMetaData metaData);
        Task ProcessChunkAsync(string connectionId, List<byte> encryptedChunk);
        Task<string> EndUploadAsync(string connectionId, Checksum checksum);
        Task CleanupConnectionAsync(string connectionId);
    }
}
