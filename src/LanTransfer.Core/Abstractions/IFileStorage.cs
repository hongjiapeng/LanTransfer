using LanTransfer.Core.Models;

namespace LanTransfer.Core.Abstractions;

public interface IFileStorage
{
    Task<UploadResult> SaveAsync(
        string originalFileName,
        Stream content,
        long length,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FileItem>> ListAsync(CancellationToken cancellationToken = default);

    Task<FileItem?> GetAsync(string fileName, CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(string fileName, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string fileName, CancellationToken cancellationToken = default);
}
