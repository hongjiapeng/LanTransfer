namespace LanTransfer.Core.Services;

public sealed class MessageValidationException : Exception
{
    public MessageValidationException(string message) : base(message)
    {
    }
}
