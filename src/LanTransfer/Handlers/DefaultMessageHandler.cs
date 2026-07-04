using System.Threading.Tasks;

namespace LanTransfer.Handlers
{
    public class DefaultMessageHandler : IMessageHandler
    {
        public Task<string> HandleMessageAsync(string message)
        {
            // Default implementation for handling incoming messages
            // Here you can add logic to process the message and return a response
            return Task.FromResult($"Received message: {message}");
        }
    }
}