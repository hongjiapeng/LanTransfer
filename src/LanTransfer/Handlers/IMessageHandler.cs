using System.Threading.Tasks;

namespace LanTransfer.Handlers
{
    public interface IMessageHandler
    {
        Task<string> HandleMessageAsync(string message);
    }
}