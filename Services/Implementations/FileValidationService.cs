using System.Security.Cryptography;
using System.Text;
using WarpBootstrap.Models;
using WarpBootstrap.Services.Interfaces;

namespace WarpBootstrap.Services.Implementations
{
    public class FileValidationService : IFileValidationService
    {
        private static readonly Lazy<FileValidationService> _instance = new(() => new FileValidationService());
        public static FileValidationService Instance => _instance.Value;

        private readonly string[] _allowedExtensions = { ".zip", ".tar", ".gz", ".7z" };
        private readonly long _maxFileSize = 100 * 1024 * 1024; // 100MB
        private readonly HashSet<char> _invalidChars;

        private FileValidationService()
        {
            _invalidChars = new HashSet<char>(Path.GetInvalidFileNameChars());
            _invalidChars.UnionWith(new[] { '<', '>', ':', '"', '|', '?', '*', '/', '\\' });
        }

        public bool ValidateFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            if (fileName.Length > 255)
                return false;

            if (fileName.Any(c => _invalidChars.Contains(c)))
                return false;

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return _allowedExtensions.Contains(extension);
        }

        public bool ValidateFileSize(long fileSize)
        {
            return fileSize > 0 && fileSize <= _maxFileSize;
        }

        public bool ValidateChecksum(string filePath, string expectedChecksum)
        {
            if (!File.Exists(filePath) || string.IsNullOrWhiteSpace(expectedChecksum))
                return false;

            var actualChecksum = CalculateFileChecksum(filePath);
            return string.Equals(actualChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase);
        }

        public string GenerateSafeFileName(string originalFileName)
        {
            var extension = Path.GetExtension(originalFileName);
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(originalFileName);

            // Remove invalid characters
            var safeNameBuilder = new StringBuilder();
            foreach (var c in nameWithoutExtension)
            {
                if (!_invalidChars.Contains(c))
                    safeNameBuilder.Append(c);
            }

            var safeName = safeNameBuilder.ToString();
            if (string.IsNullOrWhiteSpace(safeName))
                safeName = "upload";

            return $"{safeName}_{Guid.NewGuid():N}{extension}";
        }

        public ValidationResult ValidateUpload(UploadMetaData metaData)
        {
            if (!ValidateFileName(metaData.FileName))
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Invalid file name or unsupported file type"
                };
            }

            if (!ValidateFileSize(metaData.FileSize))
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"File size exceeds maximum allowed size of {_maxFileSize / (1024 * 1024)}MB"
                };
            }

            return new ValidationResult
            {
                IsValid = true,
                SafeFileName = GenerateSafeFileName(metaData.FileName)
            };
        }

        private string CalculateFileChecksum(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hashBytes = sha256.ComputeHash(stream);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}
