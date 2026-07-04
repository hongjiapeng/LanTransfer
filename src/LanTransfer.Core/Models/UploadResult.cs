namespace LanTransfer.Core.Models;

public sealed record UploadResult(
    string FileName,
    long Size,
    string DownloadUrl);
