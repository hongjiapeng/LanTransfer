using System.Threading.Tasks;

namespace FileTransferAssistant.Handlers
{
    public interface IMessageHandler
    {
        Task<string> HandleMessageAsync(string message);
    }
}