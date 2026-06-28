using FileTransferAssistant.Handlers;
using FileTransferAssistant.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;

namespace FileTransferAssistant
{
    /// <summary>
    /// Hosts a browser-based LAN file transfer service.
    /// </summary>
    public class FileTransferService : IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly ILogger<FileTransferService> _logger;
        private readonly IMessageHandler _messageHandler;
        private readonly IFileUploadHandler _fileUploadHandler;
        private readonly ServiceConfiguration _configuration;
        private IWebHost? _webHost;
        private bool _isRunning;

        public FileTransferService(
            ILogger<FileTransferService> logger,
            IMessageHandler? messageHandler = null,
            IFileUploadHandler? fileUploadHandler = null,
            ServiceConfiguration? configuration = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? new ServiceConfiguration();
            _messageHandler = messageHandler ?? new DefaultMessageHandler();
            _fileUploadHandler = fileUploadHandler ?? new DefaultFileUploadHandler(null, _configuration.StorageDirectory);

            _logger.LogInformation("FileTransferService initialized");
        }

        public event Action<string>? OnStatusChanged;

        public bool IsRunning => _isRunning;

        public int Port => _configuration.Port;

        public string StorageDirectory => _configuration.StorageDirectory;

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

                var serverUrl = GetServerUrl();
                _logger.LogInformation($"File transfer service started on {serverUrl}");
                OnStatusChanged?.Invoke($"Service started on {serverUrl}");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start file transfer service");
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
                if (_webHost != null)
                {
                    await _webHost.StopAsync();
                    _webHost.Dispose();
                    _webHost = null;
                }

                _isRunning = false;
                _logger.LogInformation("File transfer service stopped");
                OnStatusChanged?.Invoke("Service stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping file transfer service");
            }
        }

        public string GetServerUrl()
        {
            return $"http://{GetLocalIPAddress()}:{_configuration.Port}";
        }

        private void ConfigureApp(IApplicationBuilder app)
        {
            var wwwrootPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");
            _logger.LogInformation($"Serving static files from: {wwwrootPath}");

            if (!Directory.Exists(wwwrootPath))
            {
                Directory.CreateDirectory(wwwrootPath);
                _logger.LogWarning($"wwwroot directory was missing and has been created at: {wwwrootPath}");
            }

            var fileProvider = new PhysicalFileProvider(wwwrootPath);

            app.UseDefaultFiles(new DefaultFilesOptions
            {
                FileProvider = fileProvider,
                DefaultFileNames = new List<string> { "index.html" }
            });

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = fileProvider,
                RequestPath = string.Empty,
                ServeUnknownFileTypes = false
            });

            app.Run(async context =>
            {
                var request = context.Request;
                var response = context.Response;
                var path = request.Path.Value ?? string.Empty;

                AddCorsHeaders(response);

                if (request.Method == "OPTIONS")
                {
                    response.StatusCode = StatusCodes.Status200OK;
                    return;
                }

                if (!IsAuthorized(context))
                {
                    response.StatusCode = StatusCodes.Status401Unauthorized;
                    await WriteJsonAsync(response, new { success = false, error = "Unauthorized" });
                    return;
                }

                try
                {
                    if (path.Equals("/api/status", StringComparison.OrdinalIgnoreCase))
                    {
                        await HandleStatusAsync(context);
                    }
                    else if (path.Equals("/api/upload", StringComparison.OrdinalIgnoreCase))
                    {
                        await HandleFileUploadAsync(context);
                    }
                    else if (path.Equals("/api/files", StringComparison.OrdinalIgnoreCase))
                    {
                        await HandleListFilesAsync(context);
                    }
                    else if (path.StartsWith("/api/files/", StringComparison.OrdinalIgnoreCase) &&
                             path.EndsWith("/download", StringComparison.OrdinalIgnoreCase))
                    {
                        await HandleFileDownloadAsync(context, path);
                    }
                    else if (path.Equals("/api/send", StringComparison.OrdinalIgnoreCase))
                    {
                        await HandleSendMessageAsync(context);
                    }
                    else
                    {
                        response.StatusCode = StatusCodes.Status404NotFound;
                        await response.WriteAsync("Endpoint not found");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error handling request: {path}");
                    response.StatusCode = StatusCodes.Status500InternalServerError;
                    await WriteJsonAsync(response, new { success = false, error = ex.Message });
                }
            });
        }

        private async Task HandleStatusAsync(HttpContext context)
        {
            await WriteJsonAsync(context.Response, new
            {
                running = _isRunning,
                deviceName = _configuration.DeviceName,
                port = _configuration.Port,
                serverUrl = GetServerUrl(),
                allowFileUploads = _configuration.AllowFileUploads,
                maxFileSize = _configuration.MaxFileSize,
                maxFileSizeFormatted = FormatFileSize(_configuration.MaxFileSize),
                storageDirectory = _configuration.StorageDirectory,
                timestamp = DateTimeOffset.UtcNow
            });
        }

        private async Task HandleListFilesAsync(HttpContext context)
        {
            if (!HttpMethods.IsGet(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                await WriteJsonAsync(context.Response, new { success = false, error = "Method not allowed" });
                return;
            }

            var files = await _fileUploadHandler.ListFilesAsync(context.RequestAborted);
            await WriteJsonAsync(context.Response, new { success = true, files });
        }

        private async Task HandleFileDownloadAsync(HttpContext context, string path)
        {
            if (!HttpMethods.IsGet(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                await WriteJsonAsync(context.Response, new { success = false, error = "Method not allowed" });
                return;
            }

            var id = path.Substring("/api/files/".Length);
            id = id.Substring(0, id.Length - "/download".Length).Trim('/');

            var file = await _fileUploadHandler.GetFileAsync(id, context.RequestAborted);
            var stream = await _fileUploadHandler.OpenReadAsync(id, context.RequestAborted);
            if (file == null || stream == null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await WriteJsonAsync(context.Response, new { success = false, error = "File not found" });
                return;
            }

            context.Response.ContentType = file.ContentType;
            context.Response.ContentLength = file.Size;
            context.Response.Headers["Content-Disposition"] = $"attachment; filename*=UTF-8''{Uri.EscapeDataString(file.FileName)}";
            await using (stream)
            {
                await stream.CopyToAsync(context.Response.Body, context.RequestAborted);
            }
        }

        private async Task HandleFileUploadAsync(HttpContext context)
        {
            if (!_configuration.AllowFileUploads)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await WriteJsonAsync(context.Response, new { success = false, error = "File upload is disabled" });
                return;
            }

            if (!HttpMethods.IsPost(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                await WriteJsonAsync(context.Response, new { success = false, error = "Method not allowed" });
                return;
            }

            if (!context.Request.HasFormContentType)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await WriteJsonAsync(context.Response, new { success = false, error = "Invalid content type. Expected multipart/form-data" });
                return;
            }

            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            var uploadedFiles = form.Files;

            if (uploadedFiles.Count == 0)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await WriteJsonAsync(context.Response, new { success = false, error = "No files uploaded" });
                return;
            }

            var results = new List<object>();
            foreach (var file in uploadedFiles)
            {
                if (file.Length == 0)
                {
                    results.Add(new { fileName = file.FileName, success = false, error = "File is empty" });
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

                try
                {
                    await using var fileStream = file.OpenReadStream();
                    var storedFile = await _fileUploadHandler.HandleFileUploadAsync(
                        file.FileName,
                        fileStream,
                        file.Length,
                        file.ContentType,
                        context.RequestAborted);

                    _logger.LogInformation($"File uploaded successfully: {storedFile.FileName}");
                    results.Add(new
                    {
                        success = true,
                        file = storedFile
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error handling file upload: {file.FileName}");
                    results.Add(new { fileName = file.FileName, success = false, error = ex.Message });
                }
            }

            await WriteJsonAsync(context.Response, new { success = true, files = results });
        }

        private async Task HandleSendMessageAsync(HttpContext context)
        {
            if (!HttpMethods.IsPost(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                await WriteJsonAsync(context.Response, new { success = false, error = "Method not allowed" });
                return;
            }

            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();
            var messageData = JsonSerializer.Deserialize<Dictionary<string, string>>(body, JsonOptions);

            if (messageData == null || !messageData.TryGetValue("message", out var message))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await WriteJsonAsync(context.Response, new { success = false, error = "Missing 'message' field" });
                return;
            }

            _logger.LogInformation($"Received message from device: {message}");
            var handlerResponse = await _messageHandler.HandleMessageAsync(message);
            await WriteJsonAsync(context.Response, new { success = true, response = handlerResponse });
        }

        private bool IsAuthorized(HttpContext context)
        {
            if (!_configuration.RequireAccessToken)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(_configuration.AccessToken))
            {
                return false;
            }

            var headerToken = context.Request.Headers["X-Transfer-Token"].FirstOrDefault();
            var queryToken = context.Request.Query["token"].FirstOrDefault();
            return string.Equals(headerToken, _configuration.AccessToken, StringComparison.Ordinal) ||
                   string.Equals(queryToken, _configuration.AccessToken, StringComparison.Ordinal);
        }

        private static void AddCorsHeaders(HttpResponse response)
        {
            response.Headers["Access-Control-Allow-Origin"] = "*";
            response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
            response.Headers["Access-Control-Allow-Headers"] = "Content-Type, X-Transfer-Token";
        }

        private static async Task WriteJsonAsync(HttpResponse response, object payload)
        {
            response.ContentType = "application/json";
            await response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
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

                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                var endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint?.Address.ToString() ?? "127.0.0.1";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get local IP address, using localhost");
                return "127.0.0.1";
            }
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            var order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        public void Dispose()
        {
            StopAsync().GetAwaiter().GetResult();
        }
    }
}
