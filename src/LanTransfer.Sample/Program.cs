using Microsoft.Extensions.Logging;
using LanTransfer.Handlers;
using LanTransfer.Models;
using System;
using System.Threading.Tasks;

namespace LanTransfer.Sample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== 文件传输助手 / LanTransfer ===");
            Console.WriteLine();

            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddConsole()
                    .SetMinimumLevel(LogLevel.Information);
            });

            var serviceLogger = loggerFactory.CreateLogger<FileTransferService>();
            var fileHandlerLogger = loggerFactory.CreateLogger<DefaultFileUploadHandler>();

            var config = new ServiceConfiguration
            {
                Port = 8765,
                AllowFileUploads = true,
                MaxFileSize = 1073741824
            };

            var fileUploadHandler = new DefaultFileUploadHandler(fileHandlerLogger, config.StorageDirectory);
            var service = new FileTransferService(
                serviceLogger,
                null,
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
            Console.WriteLine($"Storage: {service.StorageDirectory}");
            Console.WriteLine();
            Console.WriteLine("Open the URL from another phone, tablet, or computer on the same LAN.");
            Console.WriteLine("Files received through the browser UI will be saved to the storage directory.");
            Console.WriteLine();
            Console.WriteLine("Press any key to stop the server...");
            Console.ReadKey();

            await service.StopAsync();
            service.Dispose();

            Console.WriteLine("Service stopped. Goodbye!");
        }
    }
}
