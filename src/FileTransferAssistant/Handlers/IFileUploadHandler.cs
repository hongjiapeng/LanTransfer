using FileTransferAssistant.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FileTransferAssistant.Handlers
{
    /// <summary>
    /// Interface for storing and reading transferred files.
    /// </summary>
    public interface IFileUploadHandler
    {
        /// <summary>
        /// Stores an uploaded file stream.
        /// </summary>
        /// <param name="fileName">The name of the uploaded file.</param>
        /// <param name="fileStream">The uploaded file stream.</param>
        /// <param name="fileSize">The uploaded file size in bytes.</param>
        /// <param name="contentType">The uploaded file content type.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task<TransferredFileInfo> HandleFileUploadAsync(
            string fileName,
            Stream fileStream,
            long fileSize,
            string contentType,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<TransferredFileInfo>> ListFilesAsync(CancellationToken cancellationToken = default);

        Task<TransferredFileInfo?> GetFileAsync(string id, CancellationToken cancellationToken = default);

        Task<Stream?> OpenReadAsync(string id, CancellationToken cancellationToken = default);
    }
}
