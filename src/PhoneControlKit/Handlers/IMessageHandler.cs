using System.Threading.Tasks;

namespace PhoneControlKit.Handlers
{
    public interface IMessageHandler
    {
        Task<string> HandleMessageAsync(string message);
    }
}