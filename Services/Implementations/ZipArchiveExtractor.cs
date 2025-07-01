using WarpBootstrap.FileProcessing.Abstractions;
using WarpBootstrap.Models;

namespace WarpBootstrap.Services.Implementations
{
    public class ZipArchiveExtractor : IArchiveExtractor
    {
        public Task<ExtractionResult> ExtractAsync(string archivePath, string destinationPath, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
