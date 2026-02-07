using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using PhoneControlKit.Handlers;
using PhoneControlKit.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PhoneControlKit
{
    /// <summary>
    /// Service that hosts an HTTP server for phone-to-PC communication
    /// Enables mobile devices to interact with the agent via QR code scanning
    /// </summary>
    public class PhoneControlService : IDisposable
    {
        private readonly ILogger<PhoneControlService> _logger;
        private readonly IMessageHandler _messageHandler;
        private readonly IFileUploadHandler _fileUploadHandler;
        private readonly ServiceConfiguration _configuration;
        private IWebHost _webHost;
        private bool _isRunning;
        private readonly ConcurrentDictionary<string, PendingResponse> _pendingResponses;
        private readonly SemaphoreSlim _responseSemaphore;

        /// <summary>
        /// Event fired when service status changes
        /// </summary>
        public event Action<string> OnStatusChanged;

        public bool IsRunning => _isRunning;
        public int Port => _configuration.Port;

        private class PendingResponse
        {
            public string Response { get; set; }
            public TaskCompletionSource<bool> CompletionSource { get; set; }
        }

        /// <summary>
        /// Creates a new instance of PhoneControlService
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <param name="messageHandler">Handler for incoming messages</param>
        /// <param name="fileUploadHandler">Handler for file uploads (optional)</param>
        /// <param name="configuration">Service configuration (optional)</param>
        public PhoneControlService(
            ILogger<PhoneControlService> logger,
            IMessageHandler messageHandler,
            IFileUploadHandler fileUploadHandler = null,
            ServiceConfiguration configuration = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
            _fileUploadHandler = fileUploadHandler;
            _configuration = configuration ?? new ServiceConfiguration();
            _pendingResponses = new ConcurrentDictionary<string, PendingResponse>();
            _responseSemaphore = new SemaphoreSlim(1, 1);

            _logger.LogInformation("PhoneControlService initialized");
        }

        /// <summary>
        /// Starts the HTTP server on the specified port
        /// </summary>
        public async Task<bool> StartAsync(int? port = null)
        {
            if (_isRunning)
            {
                _logger.LogWarning("Service is already running");
                return false;
            }

            if (port.HasValue)
            {
                _configuration.Port = port.Value;
            }

            try
            {
                _webHost = new WebHostBuilder()
                    .UseKestrel(options =>
                    {
                        options.Listen(IPAddress.Any, _configuration.Port);
                        options.Limits.MaxRequestBodySize = _configuration.MaxFileSize;
                        options.Limits.RequestHeadersTimeout = _configuration.RequestHeadersTimeout;
                    })
                    .Configure(ConfigureApp)
                    .Build();

                await _webHost.StartAsync();
                _isRunning = true;

                var localIp = GetLocalIPAddress();
                _logger.LogInformation($"Phone control service started on http://{localIp}:{_configuration.Port}");
                OnStatusChanged?.Invoke($"Service started on {localIp}:{_configuration.Port}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start phone control service");
                OnStatusChanged?.Invoke($"Failed to start: {ex.Message}");
                return false;
            }
        }

        public async Task StopAsync()
        {
            if (!_isRunning)
            {
                return;
            }

            try
            {
                await _webHost.StopAsync();
                _webHost.Dispose();
                _webHost = null;
                _isRunning = false;

                _logger.LogInformation("Phone control service stopped");
                OnStatusChanged?.Invoke("Service stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping phone control service");
            }
        }

        /// <summary>
        /// Gets the local IP address for QR code generation
        /// </summary>
        public string GetServerUrl()
        {
            var localIp = GetLocalIPAddress();
            return $"http://{localIp}:{_configuration.Port}";
        }

        /// <summary>
        /// Configures the ASP.NET Core application pipeline
        /// </summary>
        private void ConfigureApp(IApplicationBuilder app)
        {
            // Use physical file provider for wwwroot
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var wwwrootPath = Path.Combine(baseDirectory, "wwwroot");

            _logger.LogInformation($"Serving static files from: {wwwrootPath}");

            if (!Directory.Exists(wwwrootPath))
            {
                _logger.LogWarning($"wwwroot directory not found at: {wwwrootPath}");
            }

            var fileProvider = new PhysicalFileProvider(wwwrootPath);

            // Serve default files (index.html)
            app.UseDefaultFiles(new DefaultFilesOptions
            {
                FileProvider = fileProvider,
                DefaultFileNames = new List<string> { "index.html" }
            });

            // Serve static files
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = fileProvider,
                RequestPath = "",
                ServeUnknownFileTypes = false
            });

            app.Run(async (context) =>
            {
                var request = context.Request;
                var response = context.Response;
                string path = request.Path.Value;

                try
                {
                    response.Headers.Add("Access-Control-Allow-Origin", "*");
                    response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                    response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                    if (request.Method == "OPTIONS")
                    {
                        response.StatusCode = 200;
                        return;
                    }

                    switch (path)
                    {
                        case "/api/send":
                            await HandleSendMessageAsync(context);
                            break;

                        case "/api/upload":
                            await HandleFileUploadAsync(context);
                            break;

                        case "/api/status":
                            await HandleStatusAsync(context);
                            break;

                        default:
                            response.StatusCode = 404;
                            await response.WriteAsync("Endpoint not found");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error handling request: {path}");
                    response.StatusCode = 500;
                    await response.WriteAsync($"Internal error: {ex.Message}");
                }
            });
        }

        private async Task HandleSendMessageAsync(HttpContext context)
        {
            try
            {
                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();

                var messageData = JsonSerializer.Deserialize<Dictionary<string, string>>(body);
                if (!messageData.TryGetValue("message", out var message))
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync("Missing 'message' field");
                    return;
                }

                _logger.LogInformation($"Received message from phone: {message}");

                // Use injected message handler
                string agentResponse = await _messageHandler.HandleMessageAsync(message);

                context.Response.ContentType = "application/json";
                var responseJson = JsonSerializer.Serialize(new
                {
                    success = true,
                    response = agentResponse
                });

                await context.Response.WriteAsync(responseJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling send message");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    success = false,
                    error = ex.Message
                }));
            }
        }

        /// <summary>
        /// Handles status check requests
        /// </summary>
        private async Task HandleStatusAsync(HttpContext context)
        {
            var statusJson = JsonSerializer.Serialize(new
            {
                running = _isRunning,
                port = _configuration.Port,
                timestamp = DateTime.UtcNow
            });

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(statusJson);
        }

        private string GetLocalIPAddress()
        {
            try
            {
                var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                 ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);

                foreach (var networkInterface in networkInterfaces)
                {
                    var properties = networkInterface.GetIPProperties();
                    var ipAddress = properties.UnicastAddresses
                        .FirstOrDefault(ua => ua.Address.AddressFamily == AddressFamily.InterNetwork &&
                                             !IPAddress.IsLoopback(ua.Address))?.Address;

                    if (ipAddress != null)
                    {
                        return ipAddress.ToString();
                    }
                }

                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    var endPoint = socket.LocalEndPoint as IPEndPoint;
                    return endPoint?.Address.ToString() ?? "127.0.0.1";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get local IP address, using localhost");
                return "127.0.0.1";
            }
        }

        private async Task HandleFileUploadAsync(HttpContext context)
        {
            try
            {
                if (_fileUploadHandler == null)
                {
                    context.Response.StatusCode = 501;
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = "File upload is not supported (no handler configured)"
                    }));
                    return;
                }

                if (!_configuration.AllowFileUploads)
                {
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = "File upload is disabled"
                    }));
                    return;
                }

                if (context.Request.Method != "POST")
                {
                    context.Response.StatusCode = 405;
                    await context.Response.WriteAsync("Method not allowed");
                    return;
                }

                if (!context.Request.HasFormContentType)
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync("Invalid content type. Expected multipart/form-data");
                    return;
                }

                var form = await context.Request.ReadFormAsync();
                var uploadedFiles = form.Files;

                if (uploadedFiles.Count == 0)
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync(JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = "No files uploaded"
                    }));
                    return;
                }

                var results = new List<object>();

                foreach (var file in uploadedFiles)
                {
                    try
                    {
                        if (file.Length == 0)
                        {
                            results.Add(new
                            {
                                fileName = file.FileName,
                                success = false,
                                error = "File is empty"
                            });
                            continue;
                        }

                        if (file.Length > _configuration.MaxFileSize)
                        {
                            results.Add(new
                            {
                                fileName = file.FileName,
                                success = false,
                                error = $"File size exceeds {FormatFileSize(_configuration.MaxFileSize)} limit"
                            });
                            continue;
                        }

                        // Read file data
                        using var memoryStream = new MemoryStream();
                        await file.CopyToAsync(memoryStream);
                        var fileData = memoryStream.ToArray();

                        // Use injected file upload handler
                        await _fileUploadHandler.HandleFileUploadAsync(file.FileName, fileData);

                        _logger.LogInformation($"File uploaded successfully: {file.FileName}");

                        results.Add(new
                        {
                            fileName = file.FileName,
                            success = true,
                            size = FormatFileSize(file.Length)
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error handling file upload: {file.FileName}");
                        results.Add(new
                        {
                            fileName = file.FileName,
                            success = false,
                            error = ex.Message
                        });
                    }
                }

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    success = true,
                    files = results
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling file upload");
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    success = false,
                    error = ex.Message
                }));
            }
        }

        /// <summary>
        /// Formats file size in human-readable format
        /// </summary>
        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        public void Dispose()
        {
            StopAsync().Wait();
            _responseSemaphore?.Dispose();
        }
    }
}