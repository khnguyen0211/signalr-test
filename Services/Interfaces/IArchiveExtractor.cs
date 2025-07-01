using WarpBootstrap.Models;

namespace WarpBootstrap.FileProcessing.Abstractions
{
    public interface IArchiveExtractor
    {
        Task<ExtractionResult> ExtractAsync(string archivePath, string destinationPath, CancellationToken cancellationToken = default);

    }
}
