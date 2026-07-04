# LanTransfer

[中文文档](./README.zh-CN.md)

LanTransfer is a cross-platform LAN file transfer tool powered by .NET and a browser-based UI. Run it on one device, open the shown URL from another phone, tablet, or computer on the same local network, then upload and download files through the web page.

## Features

- Browser-based LAN file upload and download
- ASP.NET Core Kestrel receiver with a static HTML/CSS/JavaScript UI
- Chat-style file transfer interface for phones, tablets, and desktops
- Streaming file saves with temporary `.uploading` files
- Safe file-name handling, path traversal protection, and readable duplicate names
- Configurable port, storage directory, upload size limit, and optional access token
- Lightweight English and Simplified Chinese web UI localization

## Platform Support

Receiver:

- Windows, macOS, or Linux with .NET 10

Sender:

- Any modern browser on Windows, macOS, Linux, Android, or iOS

Network:

- Devices must be on the same local network unless you configure VPN, tunneling, or routing yourself.

## Quick Start

```bash
dotnet run --project src/LanTransfer.Host
```

Open `http://<receiver-ip>:8765` from another device on the same LAN.

## Configuration

LanTransfer reads the `LanTransfer` section from `src/LanTransfer.Host/appsettings.json`.

```json
{
  "LanTransfer": {
    "Port": 8765,
    "StorageDirectory": "uploads",
    "MaxFileSizeBytes": 1073741824,
    "AccessToken": null
  }
}
```

If `AccessToken` is configured, protected API calls must include `X-LanTransfer-Token` or `?token=...`.

## API Overview

- `GET /api/health` returns service status and device name.
- `POST /api/files/upload` uploads one multipart file field named `file`.
- `GET /api/files` lists received files.
- `GET /api/files/{fileName}` downloads a received file.

Error responses use stable `errorCode` values such as `file_too_large`, `file_not_found`, `invalid_file_name`, `unauthorized`, `upload_failed`, and `network_error`.

## Build from Source

```bash
dotnet build LanTransfer.sln
dotnet test LanTransfer.sln
```

## Project Structure

```text
LanTransfer/
├─ src/
│  ├─ LanTransfer.Core/
│  └─ LanTransfer.Host/
├─ tests/
│  └─ LanTransfer.Tests/
├─ docs/
├─ screenshots/
├─ README.md
├─ README.zh-CN.md
└─ LanTransfer.sln
```

## Roadmap

- Pairing code flow
- QR code for opening the LAN URL
- Delete and rename actions for received files
- Desktop tray app and notifications
- Stronger authentication for non-trusted networks

## License

MIT
