using LanTransfer.Core.Models;

namespace LanTransfer.Core.Services;

public sealed class FileStorageException : Exception
{
    public FileStorageException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }

    public ErrorResult ToErrorResult() => new(ErrorCode, Message);
}
