using System;

namespace FileTransferAssistant.Models
{
    /// <summary>
    /// Represents the configuration settings for the FileTransferService.
    /// </summary>
    public class ServiceConfiguration
    {
        /// <summary>
        /// Gets or sets the port number for the service.
        /// </summary>
        public int Port { get; set; } = 8765; // Default port

        /// <summary>
        /// Gets or sets a value indicating whether the service should allow file uploads.
        /// </summary>
        public bool AllowFileUploads { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum file size allowed for uploads (in bytes).
        /// </summary>
        public long MaxFileSize { get; set; } = 1073741824; // 1 GB

        /// <summary>
        /// Gets or sets the timeout duration for request headers.
        /// </summary>
        public TimeSpan RequestHeadersTimeout { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Gets or sets the directory where received files are stored.
        /// </summary>
        public string StorageDirectory { get; set; } = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "FileTransferAssistant"
        );

        /// <summary>
        /// Gets or sets the display name shown to devices on the same network.
        /// </summary>
        public string DeviceName { get; set; } = Environment.MachineName;

        /// <summary>
        /// Gets or sets whether API calls must include the configured access token.
        /// </summary>
        public bool RequireAccessToken { get; set; }

        /// <summary>
        /// Gets or sets the access token expected in the X-Transfer-Token header or token query string.
        /// </summary>
        public string AccessToken { get; set; } = string.Empty;
    }
}
