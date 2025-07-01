using System.Collections.Concurrent;
using System.Security.Cryptography;
using WarpBootstrap.Models;
using WarpBootstrap.Services.Interfaces;

namespace WarpBootstrap.Services.Implementations
{
    public class FileUploadService : IFileUploadService
    {
        private static readonly Lazy<FileUploadService> _instance = new(() => new FileUploadService());
        public static FileUploadService Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, UploadSession> _activeSessions = new();
        private readonly IFileValidationService _validationService;
        private readonly IEncryptionService _encryptionService;
        private readonly string _uploadFolder;

        private FileUploadService()
        {
            _validationService = FileValidationService.Instance;
            _encryptionService = EncryptionService.Instance;
            _uploadFolder = Path.Combine(Path.GetTempPath(), "Warp/Compress");
        }

        public string StartUploadAsync(string connectionId, UploadMetaData metaData)
        {
            // Validate input
            var validation = _validationService.ValidateUpload(metaData);
            if (!validation.IsValid)
                throw new InvalidOperationException(validation.ErrorMessage);

            // Ensure upload directory exists
            if (!Directory.Exists(_uploadFolder))
                Directory.CreateDirectory(_uploadFolder);

            // Create safe file path
            var safeFileName = validation.SafeFileName;
            var filePath = Path.Combine(_uploadFolder, $"{connectionId}_{safeFileName}");

            // Create upload session
            var session = new UploadSession
            {
                ConnectionId = connectionId,
                FileName = metaData.FileName,
                FilePath = filePath,
                FileSize = metaData.FileSize,
                FileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None),
                StartTime = DateTime.UtcNow,
                BytesReceived = 0
            };

            _activeSessions[connectionId] = session;

            return $"Server ready to receive: {metaData.FileName}";
        }

        public async Task ProcessChunkAsync(string connectionId, List<byte> encryptedChunk)
        {
            if (!_activeSessions.TryGetValue(connectionId, out var session))
                throw new InvalidOperationException("No active upload session found");

            if (session.FileStream == null)
                throw new InvalidOperationException("File stream is not available");

            try
            {
                // Decrypt chunk
                var encryptedData = encryptedChunk.ToArray();
                var decryptedData = _encryptionService.DecryptChunk(connectionId, encryptedData);

                // Write to file
                await session.FileStream.WriteAsync(decryptedData, 0, decryptedData.Length);
                session.BytesReceived += decryptedData.Length;
            }
            catch (Exception ex)
            {
                await CleanupConnectionAsync(connectionId);
                throw new InvalidOperationException($"Failed to process chunk: {ex.Message}");
            }
        }

        public async Task<string> EndUploadAsync(string connectionId, Checksum checksum)
        {
            if (!_activeSessions.TryRemove(connectionId, out var session))
                throw new InvalidOperationException("No active upload session found");

            try
            {
                if (session.FileStream != null)
                {
                    await session.FileStream.FlushAsync();
                    session.FileStream.Close();
                    session.FileStream.Dispose();
                }

                // Validate checksum
                var isValid = _validationService.ValidateChecksum(session.FilePath, checksum.ChecksumValue);
                if (!isValid)
                {
                    File.Delete(session.FilePath); // Clean up invalid file
                    throw new InvalidOperationException("Checksum validation failed");
                }

                // Calculate server-side checksum for verification
                var serverChecksum = CalculateFileChecksum(session.FilePath);

                return $"Upload completed successfully. Server checksum: {serverChecksum}";
            }
            catch (Exception _ex)
            {
                Console.WriteLine($"{_ex.Message}");
                await CleanupConnectionAsync(connectionId);
                throw;
            }
        }

        public async Task CleanupConnectionAsync(string connectionId)
        {
            if (_activeSessions.TryRemove(connectionId, out var session))
            {
                if (session.FileStream != null)
                {
                    await session.FileStream.DisposeAsync();
                }

                if (File.Exists(session.FilePath))
                {
                    try
                    {
                        File.Delete(session.FilePath);
                    }
                    catch (Exception ex)
                    {
                        // Log error but don't throw
                        Console.WriteLine($"Failed to delete file {session.FilePath}: {ex.Message}");
                    }
                }
            }

            _encryptionService.RemoveEncryptionKey(connectionId);
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
