namespace LanTransfer.Core.Models;

public sealed record FileItem(
    string FileName,
    long Size,
    DateTimeOffset LastModifiedTime,
    string DownloadUrl);
