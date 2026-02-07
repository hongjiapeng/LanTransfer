using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PhoneControlKit.Handlers
{
    /// <summary>
    /// Default file upload handler that saves files to a specified directory
    /// </summary>
    public class DefaultFileUploadHandler : IFileUploadHandler
    {
        private readonly ILogger<DefaultFileUploadHandler> _logger;
        private readonly string _uploadDirectory;

        public DefaultFileUploadHandler(ILogger<DefaultFileUploadHandler> logger, string uploadDirectory = null)
        {
            _logger = logger;
            _uploadDirectory = uploadDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "PhoneControlKitUploads"
            );

            EnsureDirectoryExists();
        }

        public async Task HandleFileUploadAsync(string fileName, byte[] fileData)
        {
            try
            {
                var safeFileName = GetSafeFileName(fileName);
                var filePath = Path.Combine(_uploadDirectory, safeFileName);

                await File.WriteAllBytesAsync(filePath, fileData);

                _logger.LogInformation($"File saved successfully: {filePath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving file: {fileName}");
                throw;
            }
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_uploadDirectory))
            {
                Directory.CreateDirectory(_uploadDirectory);
                _logger.LogInformation($"Created upload directory: {_uploadDirectory}");
            }
        }

        private string GetSafeFileName(string fileName)
        {
            fileName = Path.GetFileName(fileName);
            var invalidChars = Path.GetInvalidFileNameChars();
            fileName = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            return $"{nameWithoutExt}_{timestamp}{extension}";
        }
    }
}
