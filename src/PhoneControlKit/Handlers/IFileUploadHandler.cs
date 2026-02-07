using System.Threading.Tasks;

namespace PhoneControlKit.Handlers
{
    /// <summary>
    /// Interface for handling file uploads from the phone.
    /// </summary>
    public interface IFileUploadHandler
    {
        /// <summary>
        /// Handles the file upload process.
        /// </summary>
        /// <param name="fileName">The name of the uploaded file.</param>
        /// <param name="fileData">The byte array of the file data.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task HandleFileUploadAsync(string fileName, byte[] fileData);
    }
}