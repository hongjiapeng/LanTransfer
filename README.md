# LanTransfer

[中文文档](./README.zh-CN.md)

LanTransfer is a cross-platform LAN file and text transfer tool powered by .NET and a browser-based UI. Run `lantransfer` on one device, then scan its local QR code from another phone, tablet, or computer on the same network.

## Features

- Browser-based LAN file upload/download and plain-text notes
- Local QR connection dialog with multi-adapter address selection
- Cross-platform host named `lantransfer`
- ASP.NET Core Kestrel receiver using HTTP by default for LAN access
- Static HTML/CSS/JavaScript UI with no frontend build system
- Chat-style file transfer interface for phones, tablets, and desktops
- Streaming file saves with temporary `.uploading` files
- Safe file-name handling, path traversal protection, and readable duplicate names
- Configurable port, storage directory, upload size limit, and optional access token
- Lightweight English and Simplified Chinese web UI localization
- Default-browser launch after startup
- Native LanTransfer icon with an Open/Exit tray menu and no console window on Windows; unchanged console behavior on macOS/Linux

## Screenshot

![LanTransfer desktop UI](./screenshots/lantransfer-product-preview-en.png)

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

LanTransfer listens on `http://0.0.0.0:8765` and opens the local page in your default browser. Use **Connect new device** in the page menu to show a QR code for each detected LAN address.

Open one of these URLs:

- Same device: `http://localhost:8765`
- Another device on the LAN: `http://<receiver-ip>:8765`

The first version intentionally uses HTTP on the local network. It does not require trusting an ASP.NET Core development HTTPS certificate.

## Configuration

LanTransfer reads the `LanTransfer` section from `src/LanTransfer.Host/appsettings.json`.

```json
{
  "LanTransfer": {
    "Port": 8765,
    "StorageDirectory": null,
    "MaxFileSizeBytes": 1073741824,
    "MaxMessageLength": 4000,
    "OpenBrowserOnStart": true,
    "EnableWindowsTray": true,
    "AccessToken": null
  }
}
```

If `AccessToken` is configured, protected API calls must include `X-LanTransfer-Token` or `?token=...`.

When `StorageDirectory` is `null`, LanTransfer stores received files under the current user's local application-data directory (`%LOCALAPPDATA%\LanTransfer\uploads` on Windows). Set an absolute or app-relative path to override it. Keeping the default outside the executable directory makes package upgrades and uninstall safe for user files.

Environment variables can override configuration values by using the `LanTransfer__` prefix, for example:

```bash
LanTransfer__Port=9000 dotnet run --project src/LanTransfer.Host
```

On PowerShell:

```powershell
$env:LanTransfer__Port = "9000"
dotnet run --project src/LanTransfer.Host
```

## API Overview

- `GET /api/health` returns service status and device name.
- `GET /api/connect` returns usable LAN connection URLs.
- `GET /api/connect/qr?url=...` returns a locally generated SVG QR code.
- `GET /api/messages` lists plain-text notes.
- `POST /api/messages` sends a plain-text note.
- `POST /api/files/upload` uploads one multipart file field named `file`.
- `GET /api/files` lists received files.
- `GET /api/files/{fileName}` downloads a received file.

Error responses use stable `errorCode` values such as `file_too_large`, `invalid_file_name`, `invalid_message`, `unauthorized`, `upload_failed`, and `network_error`.

## Build from Source

```bash
dotnet build LanTransfer.sln
dotnet test LanTransfer.sln
```

Run the built executable directly:

```bash
src/LanTransfer.Host/bin/Debug/net10.0/lantransfer
```

On Windows:

```powershell
.\src\LanTransfer.Host\bin\Debug\net10.0\lantransfer.exe
```

## Release

Use the release script to test, tag, and push a new version:

```powershell
.\scripts\release.ps1 0.1.0
```

The script creates and pushes a `v0.1.0` tag. GitHub Actions then creates a GitHub Release, publishes self-contained builds, and uploads assets for Windows x64, Linux x64/ARM64, and macOS x64/ARM64.

To preview the checks without creating a tag, run:

```powershell
.\scripts\release.ps1 0.1.0 -DryRun
```

## Project Structure

```text
LanTransfer/
├─ src/
│  ├─ LanTransfer.Core/
│  └─ LanTransfer.Host/
│     └─ wwwroot/
├─ tests/
│  └─ LanTransfer.Tests/
├─ docs/
├─ screenshots/
├─ README.md
├─ README.zh-CN.md
└─ LanTransfer.sln
```

## Roadmap

- Optional expiring pairing-code approval flow
- Delete and rename actions for received files
- Better image/file thumbnails
- Desktop notifications
- Stronger authentication for non-trusted networks

## License

MIT
