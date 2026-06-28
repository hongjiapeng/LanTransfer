using FileTransferAssistant.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FileTransferAssistant.Handlers
{
    /// <summary>
    /// Default file upload handler that saves files to a specified directory
    /// </summary>
    public class DefaultFileUploadHandler : IFileUploadHandler
    {
        private const int BufferSize = 81920;
        private readonly ILogger<DefaultFileUploadHandler>? _logger;
        private readonly string _uploadDirectory;

        public DefaultFileUploadHandler(ILogger<DefaultFileUploadHandler>? logger, string? uploadDirectory = null)
        {
            _logger = logger;
            _uploadDirectory = uploadDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads",
                "FileTransferAssistant"
            );

            EnsureDirectoryExists();
        }

        public async Task<TransferredFileInfo> HandleFileUploadAsync(
            string fileName,
            Stream fileStream,
            long fileSize,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var safeFileName = GetUniqueSafeFileName(fileName);
                var filePath = Path.Combine(_uploadDirectory, safeFileName);

                await using var outputStream = new FileStream(
                    filePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.Read,
                    BufferSize,
                    useAsync: true);

                await fileStream.CopyToAsync(outputStream, BufferSize, cancellationToken);

                _logger?.LogInformation($"File saved successfully: {filePath}");
                return CreateFileInfo(filePath, contentType);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error saving file: {fileName}");
                throw;
            }
        }

        public Task<IReadOnlyList<TransferredFileInfo>> ListFilesAsync(CancellationToken cancellationToken = default)
        {
            EnsureDirectoryExists();

            IReadOnlyList<TransferredFileInfo> files = Directory
                .EnumerateFiles(_uploadDirectory)
                .Select(path => CreateFileInfo(path, GetContentType(path)))
                .OrderByDescending(file => file.ReceivedAt)
                .ToList();

            return Task.FromResult(files);
        }

        public Task<TransferredFileInfo?> GetFileAsync(string id, CancellationToken cancellationToken = default)
        {
            var filePath = ResolveFilePath(id);
            if (filePath == null || !File.Exists(filePath))
            {
                return Task.FromResult<TransferredFileInfo?>(null);
            }

            return Task.FromResult<TransferredFileInfo?>(CreateFileInfo(filePath, GetContentType(filePath)));
        }

        public Task<Stream?> OpenReadAsync(string id, CancellationToken cancellationToken = default)
        {
            var filePath = ResolveFilePath(id);
            if (filePath == null || !File.Exists(filePath))
            {
                return Task.FromResult<Stream?>(null);
            }

            Stream stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                BufferSize,
                useAsync: true);

            return Task.FromResult<Stream?>(stream);
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_uploadDirectory))
            {
                Directory.CreateDirectory(_uploadDirectory);
                _logger?.LogInformation($"Created upload directory: {_uploadDirectory}");
            }
        }

        private string GetUniqueSafeFileName(string fileName)
        {
            fileName = Path.GetFileName(fileName ?? string.Empty);
            var invalidChars = Path.GetInvalidFileNameChars();
            fileName = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "received-file";
            }

            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            var candidate = $"{nameWithoutExt}{extension}";
            var counter = 1;

            while (File.Exists(Path.Combine(_uploadDirectory, candidate)))
            {
                candidate = $"{nameWithoutExt} ({counter}){extension}";
                counter++;
            }

            return candidate;
        }

        private TransferredFileInfo CreateFileInfo(string filePath, string contentType)
        {
            var fileInfo = new FileInfo(filePath);
            return new TransferredFileInfo
            {
                Id = EncodeId(fileInfo.Name),
                FileName = fileInfo.Name,
                ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                Size = fileInfo.Length,
                FormattedSize = FormatFileSize(fileInfo.Length),
                ReceivedAt = fileInfo.LastWriteTimeUtc,
                DownloadUrl = $"/api/files/{EncodeId(fileInfo.Name)}/download"
            };
        }

        private string? ResolveFilePath(string id)
        {
            var fileName = TryDecodeId(id);
            if (fileName == null)
            {
                return null;
            }

            var safeFileName = Path.GetFileName(fileName);
            return Path.Combine(_uploadDirectory, safeFileName);
        }

        private static string EncodeId(string fileName)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(fileName))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static string? TryDecodeId(string id)
        {
            try
            {
                var base64 = id.Replace('-', '+').Replace('_', '/');
                switch (base64.Length % 4)
                {
                    case 2:
                        base64 += "==";
                        break;
                    case 3:
                        base64 += "=";
                        break;
                }

                return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private static string GetContentType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".txt" => "text/plain",
                ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".pdf" => "application/pdf",
                ".zip" => "application/zip",
                _ => "application/octet-stream"
            };
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            var order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}
