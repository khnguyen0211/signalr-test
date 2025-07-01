using WarpBootstrap.Models;

namespace WarpBootstrap.Services.Interfaces
{
    public interface IFileValidationService
    {
        bool ValidateFileName(string fileName);
        bool ValidateFileSize(long fileSize);
        bool ValidateChecksum(string filePath, string expectedChecksum);
        string GenerateSafeFileName(string originalFileName);

        ValidationResult ValidateUpload(UploadMetaData metaData);

    }
}
