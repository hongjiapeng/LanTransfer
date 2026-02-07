using System;

namespace PhoneControlKit.Models
{
    /// <summary>
    /// Event arguments for file upload notification
    /// </summary>
    public class FileUploadedEventArgs
    {
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public string FilePath { get; set; }
        public string FormattedSize { get; set; }
        public DateTime UploadTime { get; set; }
        public bool IsImage { get; set; }
    }
}