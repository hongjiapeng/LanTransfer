using LanTransfer.Core.Abstractions;
using LanTransfer.Core.Models;
using LanTransfer.Core.Options;

namespace LanTransfer.Core.Services;

public sealed class LocalFileStorage : IFileStorage
{
    private const int BufferSize = 81920;
    private readonly LanTransferOptions _options;
    private readonly string _storageRoot;

    public LocalFileStorage(LanTransferOptions options)
    {
        _options = options;
        _storageRoot = Path.GetFullPath(options.StorageDirectory);
        Directory.CreateDirectory(_storageRoot);
    }

    public async Task<UploadResult> SaveAsync(
        string originalFileName,
        Stream content,
        long length,
        CancellationToken cancellationToken = default)
    {
        if (length > _options.MaxFileSizeBytes)
        {
            throw new FileStorageException(ErrorCodes.FileTooLarge, "File is too large.");
        }

        var safeFileName = CreateSafeUploadFileName(originalFileName);
        var finalPath = GetUniquePath(safeFileName);
        EnsurePathIsInsideStorage(finalPath);

        var tempPath = Path.Combine(_storageRoot, $".{Guid.NewGuid():N}.uploading");
        var written = 0L;

        try
        {
            await using (var output = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[BufferSize];
                while (true)
                {
                    var read = await content.ReadAsync(buffer, cancellationToken);
                    if (read == 0)
                    {
                        break;
                    }

                    written += read;
                    if (written > _options.MaxFileSizeBytes)
                    {
                        throw new FileStorageException(ErrorCodes.FileTooLarge, "File is too large.");
                    }

                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }

            File.Move(tempPath, finalPath, overwrite: false);
            var fileInfo = new FileInfo(finalPath);
            return new UploadResult(fileInfo.Name, fileInfo.Length, ToDownloadUrl(fileInfo.Name));
        }
        catch
        {
            TryDeleteFile(tempPath);
            throw;
        }
    }

    public Task<IReadOnlyList<FileItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_storageRoot);

        IReadOnlyList<FileItem> files = Directory
            .EnumerateFiles(_storageRoot)
            .Where(path => !path.EndsWith(".uploading", StringComparison.OrdinalIgnoreCase))
            .Select(CreateFileItem)
            .OrderByDescending(file => file.LastModifiedTime)
            .ThenBy(file => file.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult(files);
    }

    public Task<FileItem?> GetAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var path = ResolveExistingFilePath(fileName);
        return Task.FromResult(path is null ? null : CreateFileItem(path));
    }

    public Task<Stream?> OpenReadAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var path = ResolveExistingFilePath(fileName);
        if (path is null)
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return Task.FromResult<Stream?>(stream);
    }

    public Task<bool> DeleteAsync(string fileName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var path = ResolveExistingFilePath(fileName);
        if (path is null)
        {
            return Task.FromResult(false);
        }

        var trashRoot = Path.Combine(_storageRoot, ".lantransfer", "trash");
        var trashEntry = Path.Combine(
            trashRoot,
            $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(trashEntry);
        File.Move(path, Path.Combine(trashEntry, Path.GetFileName(path)), overwrite: false);
        return Task.FromResult(true);
    }

    private string? ResolveExistingFilePath(string fileName)
    {
        var safeFileName = RequireSafeLookupFileName(fileName);
        var fullPath = Path.GetFullPath(Path.Combine(_storageRoot, safeFileName));
        EnsurePathIsInsideStorage(fullPath);

        return File.Exists(fullPath) ? fullPath : null;
    }

    private string GetUniquePath(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var candidate = fileName;
        var index = 1;

        while (File.Exists(Path.Combine(_storageRoot, candidate)))
        {
            candidate = $"{name} ({index}){extension}";
            index++;
        }

        return Path.GetFullPath(Path.Combine(_storageRoot, candidate));
    }

    private string CreateSafeUploadFileName(string fileName)
    {
        var decoded = DecodeFileName(fileName);
        RejectPathLikeFileName(decoded);

        var invalidChars = Path.GetInvalidFileNameChars()
            .Concat(new[] { ':', '*', '?', '"', '<', '>', '|' })
            .ToHashSet();

        var cleaned = new string(decoded
            .Trim()
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray())
            .Trim(' ', '.');

        return string.IsNullOrWhiteSpace(cleaned) ? "file" : cleaned;
    }

    private string RequireSafeLookupFileName(string fileName)
    {
        var decoded = DecodeFileName(fileName);
        RejectPathLikeFileName(decoded);

        var trimmed = decoded.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Trim('.').Length == 0)
        {
            throw new FileStorageException(ErrorCodes.InvalidFileName, "File name is invalid.");
        }

        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new FileStorageException(ErrorCodes.InvalidFileName, "File name is invalid.");
        }

        return trimmed;
    }

    private void EnsurePathIsInsideStorage(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = _storageRoot.EndsWith(Path.DirectorySeparatorChar)
            ? _storageRoot
            : _storageRoot + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new FileStorageException(ErrorCodes.InvalidFileName, "File name is invalid.");
        }
    }

    private static void RejectPathLikeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) ||
            Path.IsPathFullyQualified(fileName) ||
            fileName.Contains('/') ||
            fileName.Contains('\\'))
        {
            throw new FileStorageException(ErrorCodes.InvalidFileName, "File name is invalid.");
        }
    }

    private static string DecodeFileName(string fileName)
    {
        try
        {
            return Uri.UnescapeDataString(fileName ?? string.Empty);
        }
        catch (UriFormatException)
        {
            throw new FileStorageException(ErrorCodes.InvalidFileName, "File name is invalid.");
        }
    }

    private static FileItem CreateFileItem(string path)
    {
        var fileInfo = new FileInfo(path);
        return new FileItem(
            fileInfo.Name,
            fileInfo.Length,
            fileInfo.LastWriteTimeUtc,
            ToDownloadUrl(fileInfo.Name));
    }

    private static string ToDownloadUrl(string fileName)
    {
        return $"/api/files/{Uri.EscapeDataString(fileName)}";
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Cleanup is best effort after a failed upload.
        }
    }
}
