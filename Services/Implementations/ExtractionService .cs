using System.Collections.Concurrent;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using WarpBootstrap.Models;
using WarpBootstrap.Services.Interfaces;

namespace WarpBootstrap.Services.Implementations
{
    public class ExtractionService : IExtractionService
    {
        private static readonly Lazy<ExtractionService> _instance = new(() => new ExtractionService());
        public static ExtractionService Instance => _instance.Value;

        private readonly string _extractBaseFolder;
        private readonly ConcurrentDictionary<string, ExtractionProgress> _extractionProgress = new();

        // Supported file extensions
        private readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".tar.gz", ".tar.bz2", ".tgz"
        };

        private ExtractionService()
        {
            _extractBaseFolder = Path.Combine(Path.GetTempPath(), "Warp", "Extract");
            EnsureDirectoryExists(_extractBaseFolder);
        }

        public async Task<ExtractionResult> ExtractArchiveAsync(string archivePath, string connectionId)
        {
            var result = new ExtractionResult
            {
                ExtractionTime = DateTime.UtcNow
            };

            try
            {
                if (!File.Exists(archivePath))
                    throw new FileNotFoundException($"Archive file not found: {archivePath}");

                var fileName = Path.GetFileNameWithoutExtension(archivePath);
                var extractPath = GetExtractionPath(connectionId, fileName);

                EnsureDirectoryExists(extractPath);
                result.ExtractionPath = extractPath;

                // Initialize progress tracking
                var progress = new ExtractionProgress();
                _extractionProgress[connectionId] = progress;

                // Extract based on file type
                if (archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
                    archivePath.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
                {
                    await ExtractTarGzAsync(archivePath, extractPath, progress);
                }
                else
                {
                    await ExtractGeneralArchiveAsync(archivePath, extractPath, progress);
                }

                // Collect extraction results
                result.ExtractedFiles = await GetExtractedFilesAsync(extractPath);
                result.FileCount = result.ExtractedFiles.Count(f => !f.IsDirectory);
                result.TotalSize = result.ExtractedFiles.Sum(f => f.Size);
                result.Success = true;

                // Remove progress tracking
                _extractionProgress.TryRemove(connectionId, out _);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                Console.WriteLine($"[Extraction Error] {ex.Message}");
                _extractionProgress.TryRemove(connectionId, out _);
            }

            return result;
        }

        private async Task ExtractGeneralArchiveAsync(string archivePath, string extractPath, ExtractionProgress progress)
        {
            await Task.Run(() =>
            {
                using var archive = ArchiveFactory.Open(archivePath);

                progress.TotalEntries = archive.Entries.Count();
                progress.TotalBytes = archive.TotalUncompressSize;

                var options = new ExtractionOptions
                {
                    ExtractFullPath = true,
                    Overwrite = true,
                    PreserveFileTime = true
                };

                foreach (var entry in archive.Entries)
                {
                    if (!entry.IsDirectory)
                    {
                        progress.CurrentFile = entry.Key;

                        // Extract entry
                        entry.WriteToDirectory(extractPath, options);

                        progress.ProcessedEntries++;
                        progress.ProcessedBytes += entry.Size;
                    }
                }
            });
        }

        private async Task ExtractTarGzAsync(string archivePath, string extractPath, ExtractionProgress progress)
        {
            await Task.Run(() =>
            {
                using var stream = File.OpenRead(archivePath);
                using var reader = ReaderFactory.Open(stream);

                var entries = new List<IEntry>();

                // First pass: count entries
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                        entries.Add(reader.Entry);
                }

                progress.TotalEntries = entries.Count;

                // Reset stream for extraction
                stream.Seek(0, SeekOrigin.Begin);
                using var extractReader = ReaderFactory.Open(stream);

                while (extractReader.MoveToNextEntry())
                {
                    if (!extractReader.Entry.IsDirectory)
                    {
                        progress.CurrentFile = extractReader.Entry.Key;

                        var destinationPath = Path.Combine(extractPath, extractReader.Entry.Key);
                        var destinationDir = Path.GetDirectoryName(destinationPath);

                        if (!string.IsNullOrEmpty(destinationDir))
                            EnsureDirectoryExists(destinationDir);

                        extractReader.WriteEntryToFile(destinationPath, new ExtractionOptions
                        {
                            ExtractFullPath = true,
                            Overwrite = true
                        });

                        progress.ProcessedEntries++;
                        progress.ProcessedBytes += extractReader.Entry.Size;
                    }
                }
            });
        }

        public async Task<ArchiveInfo> GetArchiveInfoAsync(string archivePath)
        {
            var info = new ArchiveInfo();

            try
            {
                await Task.Run(() =>
                {
                    var fileInfo = new FileInfo(archivePath);
                    info.CompressedSize = fileInfo.Length;

                    using var archive = ArchiveFactory.Open(archivePath);

                    info.ArchiveType = archive.Type.ToString();
                    info.UncompressedSize = archive.TotalUncompressSize;
                    info.EntryCount = archive.Entries.Count();
                    info.IsPasswordProtected = archive.Entries.Any(e => e.IsEncrypted);

                    foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                    {
                        info.FileList.Add(entry.Key);
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Archive Info Error] {ex.Message}");
                throw;
            }

            return info;
        }

        public bool IsSupportedArchive(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            // Special case for .tar.gz
            if (fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
                return true;

            return _supportedExtensions.Contains(extension);
        }

        public string GetExtractionPath(string connectionId, string fileName)
        {
            var timestamp = DateTime.UtcNow.Ticks;
            var safeName = Path.GetFileNameWithoutExtension(fileName);
            return Path.Combine(_extractBaseFolder, connectionId, $"{timestamp}_{safeName}");
        }

        public async Task CleanupExtractionAsync(string connectionId)
        {
            var connectionPath = Path.Combine(_extractBaseFolder, connectionId);

            if (Directory.Exists(connectionPath))
            {
                await Task.Run(() =>
                {
                    try
                    {
                        Directory.Delete(connectionPath, recursive: true);
                        Console.WriteLine($"[Cleanup] Removed extraction folder for connection: {connectionId}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Cleanup Error] Failed to remove folder: {ex.Message}");
                    }
                });
            }
        }

        private async Task<List<ExtractedFile>> GetExtractedFilesAsync(string extractPath)
        {
            var files = new List<ExtractedFile>();

            await Task.Run(() =>
            {
                var directoryInfo = new DirectoryInfo(extractPath);
                var allItems = directoryInfo.GetFileSystemInfos("*", SearchOption.AllDirectories);

                foreach (var item in allItems)
                {
                    var relativePath = Path.GetRelativePath(extractPath, item.FullName);

                    files.Add(new ExtractedFile
                    {
                        FileName = item.Name,
                        RelativePath = relativePath,
                        FullPath = item.FullName,
                        Size = item is FileInfo fileInfo ? fileInfo.Length : 0,
                        LastModified = item.LastWriteTimeUtc,
                        IsDirectory = item is DirectoryInfo
                    });
                }
            });

            return files;
        }

        private void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public ExtractionProgress? GetExtractionProgress(string connectionId)
        {
            return _extractionProgress.TryGetValue(connectionId, out var progress) ? progress : null;
        }
    }
}
