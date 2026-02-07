using Microsoft.Extensions.Logging;
using PhoneControlKit.Handlers;
using System;
using System.Threading.Tasks;

namespace PhoneControlKit.Sample
{
    /// <summary>
    /// Sample message handler that echoes messages and provides some basic responses
    /// </summary>
    public class SampleMessageHandler : IMessageHandler
    {
        private readonly ILogger<SampleMessageHandler> _logger;

        public SampleMessageHandler(ILogger<SampleMessageHandler> logger)
        {
            _logger = logger;
        }

        public async Task<string> HandleMessageAsync(string message)
        {
            _logger.LogInformation($"Processing message: {message}");

            // Simulate some processing delay
            await Task.Delay(100);

            // Provide different responses based on content
            if (message.Contains("hello", StringComparison.OrdinalIgnoreCase))
            {
                return "Hello from PC! 👋";
            }
            else if (message.Contains("time", StringComparison.OrdinalIgnoreCase))
            {
                return $"Current time: {DateTime.Now:HH:mm:ss}";
            }
            else if (message.Contains("date", StringComparison.OrdinalIgnoreCase))
            {
                return $"Current date: {DateTime.Now:yyyy-MM-dd}";
            }
            else
            {
                return $"Echo: {message}";
            }
        }
    }
}