namespace LanTransfer.Core.Options;

public sealed class LanTransferOptions
{
    public int Port { get; init; } = 8765;
    public string StorageDirectory { get; init; } = "uploads";
    public long MaxFileSizeBytes { get; init; } = 1024L * 1024 * 1024;
    public int MaxMessageLength { get; init; } = 4000;
    public bool OpenBrowserOnStart { get; init; } = true;
    public bool EnableWindowsTray { get; init; } = true;
    public string? AccessToken { get; init; }
}
