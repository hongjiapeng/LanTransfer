using System;

namespace LanTransfer.Models
{
    /// <summary>
    /// Event arguments for file upload notification
    /// </summary>
    public class FileUploadedEventArgs
    {
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string FormattedSize { get; set; } = string.Empty;
        public DateTime UploadTime { get; set; }
        public bool IsImage { get; set; }
    }
}
