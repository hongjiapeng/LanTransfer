# PhoneControlKit

A .NET library for seamless phone-to-PC communication via HTTP server. Enable mobile devices to send messages and upload files to your PC applications through a simple web interface.

## ✨ Features

- **HTTP Server**: Lightweight HTTP server for phone-to-PC communication
- **Message Handling**: Customizable message handlers with async support
- **File Uploads**: Support for file uploads with configurable size limits
- **Event Notifications**: Real-time service status updates
- **Embedded Web UI**: Built-in mobile-friendly web interface
- **Flexible Configuration**: Easy-to-configure service options
- **Dependency Injection Ready**: Designed with modern .NET patterns

## 📦 Installation

```bash
dotnet add package PhoneControlKit
```

Or clone and build from source:

```bash
git clone <repository-url>
cd PhoneControlKit
dotnet build
```

## 🚀 Quick Start

### Basic Usage

```csharp
using Microsoft.Extensions.Logging;
using PhoneControlKit;
using PhoneControlKit.Handlers;
using PhoneControlKit.Models;

// Setup logging
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<PhoneControlService>();

// Create message handler
var messageHandler = new DefaultMessageHandler();

// Create file upload handler (optional)
var fileUploadHandler = new DefaultFileUploadHandler(
    loggerFactory.CreateLogger<DefaultFileUploadHandler>()
);

// Configure service
var config = new ServiceConfiguration
{
    Port = 8765,
    AllowFileUploads = true,
    MaxFileSize = 104857600  // 100 MB
};

// Create and start service
var service = new PhoneControlService(
    logger,
    messageHandler,
    fileUploadHandler,
    config
);

await service.StartAsync();
Console.WriteLine($"Server running at: {service.GetServerUrl()}");
```

### Custom Message Handler

```csharp
public class MyMessageHandler : IMessageHandler
{
    public async Task<string> HandleMessageAsync(string message)
    {
        // Process the message from phone
        var result = await ProcessMessageAsync(message);
        
        // Return response to phone
        return $"Processed: {result}";
    }
}
```

### Custom File Upload Handler

```csharp
public class MyFileUploadHandler : IFileUploadHandler
{
    public async Task HandleFileUploadAsync(string fileName, byte[] fileData)
    {
        // Save file to custom location
        var path = Path.Combine(@"C:\MyUploads", fileName);
        await File.WriteAllBytesAsync(path, fileData);
        
        // Process file as needed
        await ProcessFileAsync(path);
    }
}
```

## 📱 Using from Phone

1. Start the service on your PC
2. Note the URL displayed (e.g., `http://192.168.1.100:8765`)
3. Open the URL in your phone's browser
4. Send messages or upload files through the web interface

## ⚙️ Configuration

```csharp
var config = new ServiceConfiguration
{
    Port = 8765,                     // Server port
    AllowFileUploads = true,         // Enable/disable file uploads
    MaxFileSize = 104857600,         // Max file size (100 MB)
    RequestHeadersTimeout = TimeSpan.FromMinutes(2)
};
```

## 🏗️ Architecture

```
PhoneControlKit
├── PhoneControlService          # Main service class
├── Handlers
│   ├── IMessageHandler          # Message processing interface
│   ├── IFileUploadHandler       # File upload interface
│   ├── DefaultMessageHandler    # Default message handler
│   └── DefaultFileUploadHandler # Default file handler
├── Models
│   ├── ServiceConfiguration     # Service config model
│   └── FileUploadedEventArgs   # File upload event args
└── wwwroot                      # Embedded web UI
    ├── index.html
    ├── css/styles.css
    └── js/app.js
```

## 🧪 Running the Sample

```bash
cd src/PhoneControlKit.Sample
dotnet run
```

## 🧪 Running Tests

```bash
cd tests/PhoneControlKit.Tests
dotnet test
```

## 📋 Requirements

- .NET 6.0 or higher
- Network connectivity between phone and PC (same network)

## 🤝 Contributing

Contributions are welcome! Please open an issue or submit a pull request.

## 📄 License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

## 💡 Use Cases

- Remote PC control from mobile
- File sharing between phone and PC
- Voice command relay
- Mobile-to-PC notifications
- Cross-device automation
- IoT device communication

## 🔒 Security Notes

- The service listens on all network interfaces by default
- Consider adding authentication for production use
- File uploads are limited by configured size limits
- Always validate and sanitize incoming data