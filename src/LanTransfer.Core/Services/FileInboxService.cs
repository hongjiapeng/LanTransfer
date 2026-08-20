using LanTransfer.Core.Abstractions;
using LanTransfer.Core.Models;

namespace LanTransfer.Core.Services;

public sealed class FileInboxService : IFileInbox
{
    private readonly IFileStorage _storage;

    public FileInboxService(IFileStorage storage)
    {
        _storage = storage;
    }

    public Task<UploadResult> SaveAsync(
        string originalFileName,
        Stream content,
        long length,
        CancellationToken cancellationToken = default)
    {
        return _storage.SaveAsync(originalFileName, content, length, cancellationToken);
    }

    public Task<IReadOnlyList<FileItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        return _storage.ListAsync(cancellationToken);
    }

    public Task<FileItem?> GetAsync(string fileName, CancellationToken cancellationToken = default)
    {
        return _storage.GetAsync(fileName, cancellationToken);
    }

    public Task<Stream?> OpenReadAsync(string fileName, CancellationToken cancellationToken = default)
    {
        return _storage.OpenReadAsync(fileName, cancellationToken);
    }

    public Task<bool> DeleteAsync(string fileName, CancellationToken cancellationToken = default)
    {
        return _storage.DeleteAsync(fileName, cancellationToken);
    }
}
