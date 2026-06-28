# FileTransferAssistant

[简体中文](README.zh-CN.md)

FileTransferAssistant is a cross-platform LAN file transfer assistant powered by .NET and a browser-based UI. Run it on one device, open the shown URL from another phone, tablet, or computer on the same local network, then send and download files through the web page.

## Features

- Browser-based file upload UI for phones, tablets, and desktop computers
- Cross-platform receiver built on .NET 10 and ASP.NET Core Kestrel
- Streaming file saves, so uploads are not buffered into memory first
- File inbox API with download links
- Configurable port, storage directory, upload size limit, and optional access token
- Small CLI sample for local testing and open-source demos

## Platform Support

Receiver:

- Windows, macOS, and Linux with .NET 10

Sender:

- Any modern browser on Windows, macOS, Linux, Android, or iOS

Network:

- Devices should be on the same LAN unless you use VPN, tunneling, or your own network routing.

## Quick Start

```bash
dotnet run --project src/FileTransferAssistant.Sample
```

Open the printed URL from another device on the same LAN.

## Library Usage

```csharp
using FileTransferAssistant;
using FileTransferAssistant.Models;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

var config = new ServiceConfiguration
{
    Port = 8765,
    AllowFileUploads = true,
    MaxFileSize = 1073741824
};

var service = new FileTransferService(
    loggerFactory.CreateLogger<FileTransferService>(),
    configuration: config
);

await service.StartAsync();
Console.WriteLine(service.GetServerUrl());
```

## Configuration

```csharp
var config = new ServiceConfiguration
{
    Port = 8765,
    DeviceName = Environment.MachineName,
    StorageDirectory = @"D:\Transfers",
    AllowFileUploads = true,
    MaxFileSize = 1073741824,
    RequestHeadersTimeout = TimeSpan.FromMinutes(2),
    RequireAccessToken = false,
    AccessToken = ""
};
```

## HTTP API

- `GET /api/status` returns device name, URL, upload limit, and storage directory.
- `POST /api/upload` accepts `multipart/form-data` files.
- `GET /api/files` lists received files.
- `GET /api/files/{id}/download` downloads a received file.
- `POST /api/send` is retained as an optional message extension point.

## Project Structure

```text
FileTransferAssistant
├── src/FileTransferAssistant
│   ├── FileTransferService.cs
│   ├── Handlers
│   ├── Models
│   └── wwwroot
├── src/FileTransferAssistant.Sample
└── tests/FileTransferAssistant.Tests
```

## Build and Test

```bash
dotnet build FileTransferAssistant.sln
dotnet test FileTransferAssistant.sln
```

## Security Notes

The service listens on all network interfaces by default so other devices on the LAN can reach it. For trusted home or office LANs this is convenient, but public or shared networks need more care.

- Use `RequireAccessToken` and `AccessToken` before exposing the service beyond a trusted LAN.
- Keep received files in a controlled storage directory.
- Do not expose the port directly to the public internet without adding stronger authentication and transport security.

## Roadmap

- Device discovery on LAN
- Pairing code flow
- Send-to-device mode for true PC-to-PC workflows
- Delete and rename actions in the inbox
- Desktop tray app and notifications

## License

MIT
