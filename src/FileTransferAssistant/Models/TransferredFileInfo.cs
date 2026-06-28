using System;

namespace FileTransferAssistant.Models
{
    /// <summary>
    /// Describes a file received by the assistant.
    /// </summary>
    public class TransferredFileInfo
    {
        public string Id { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
        public long Size { get; set; }
        public string FormattedSize { get; set; } = string.Empty;
        public DateTimeOffset ReceivedAt { get; set; }
        public string DownloadUrl { get; set; } = string.Empty;
    }
}
