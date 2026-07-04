namespace LanTransfer.Core.Models;

public sealed record ErrorResult(string ErrorCode, string Message)
{
    public static ErrorResult FileTooLarge() => new(ErrorCodes.FileTooLarge, "File is too large.");

    public static ErrorResult FileNotFound() => new(ErrorCodes.FileNotFound, "File was not found.");

    public static ErrorResult InvalidFileName() => new(ErrorCodes.InvalidFileName, "File name is invalid.");

    public static ErrorResult Unauthorized() => new(ErrorCodes.Unauthorized, "Unauthorized.");

    public static ErrorResult UploadFailed() => new(ErrorCodes.UploadFailed, "File upload failed.");

    public static ErrorResult NetworkError() => new(ErrorCodes.NetworkError, "Network error.");
}

public static class ErrorCodes
{
    public const string FileTooLarge = "file_too_large";
    public const string FileNotFound = "file_not_found";
    public const string InvalidFileName = "invalid_file_name";
    public const string Unauthorized = "unauthorized";
    public const string UploadFailed = "upload_failed";
    public const string NetworkError = "network_error";
}
