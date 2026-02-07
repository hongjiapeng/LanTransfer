using Microsoft.Extensions.Logging;
using PhoneControlKit.Handlers;
using PhoneControlKit.Models;
using System;
using System.Threading.Tasks;

namespace PhoneControlKit.Sample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== PhoneControlKit Sample ===");
            Console.WriteLine();

            // Setup logging
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole()
                    .SetMinimumLevel(LogLevel.Information);
            });

            var serviceLogger = loggerFactory.CreateLogger<PhoneControlService>();
            var handlerLogger = loggerFactory.CreateLogger<SampleMessageHandler>();
            var fileHandlerLogger = loggerFactory.CreateLogger<DefaultFileUploadHandler>();

            // Create handlers
            var messageHandler = new SampleMessageHandler(handlerLogger);
            var fileUploadHandler = new DefaultFileUploadHandler(fileHandlerLogger);

            // Configure service
            var config = new ServiceConfiguration
            {
                Port = 8765,
                AllowFileUploads = true,
                MaxFileSize = 104857600  // 100 MB
            };

            // Create and start service
            var service = new PhoneControlService(
                serviceLogger,
                messageHandler,
                fileUploadHandler,
                config
            );

            service.OnStatusChanged += (status) =>
            {
                Console.WriteLine($"[Status] {status}");
            };

            var started = await service.StartAsync();
            if (!started)
            {
                Console.WriteLine("Failed to start service");
                return;
            }

            Console.WriteLine();
            Console.WriteLine($"Server URL: {service.GetServerUrl()}");
            Console.WriteLine();
            Console.WriteLine("🔗 Scan this URL with your phone's browser or QR code scanner");
            Console.WriteLine("📱 You can now send messages and upload files from your phone");
            Console.WriteLine();
            Console.WriteLine("Press any key to stop the server...");
            Console.ReadKey();

            await service.StopAsync();
            service.Dispose();

            Console.WriteLine("Service stopped. Goodbye!");
        }
    }
}