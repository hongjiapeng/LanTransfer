using System;

namespace PhoneControlKit.Models
{
    /// <summary>
    /// Represents the configuration settings for the PhoneControlService.
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
        public long MaxFileSize { get; set; } = 104857600; // 100 MB

        /// <summary>
        /// Gets or sets the timeout duration for request headers.
        /// </summary>
        public TimeSpan RequestHeadersTimeout { get; set; } = TimeSpan.FromMinutes(2);
    }
}