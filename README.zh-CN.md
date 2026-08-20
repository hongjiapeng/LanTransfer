# LanTransfer

[English](./README.md)

LanTransfer 是一个跨平台局域网文件和文字传输工具。在一台设备上启动 `lantransfer` 后，可通过页面中的局域网二维码让手机、平板或另一台电脑快速打开连接。

在同一局域网内，在手机、平板和电脑之间快速传输文件。

## 功能特性

- 基于浏览器的局域网文件上传、下载和纯文字备注
- 本地生成连接二维码，并支持在多网卡地址间切换
- 名为 `lantransfer` 的跨平台 Host
- 基于 ASP.NET Core Kestrel 的接收端服务，默认使用局域网 HTTP
- 纯 HTML/CSS/JavaScript 静态页面，不需要前端构建系统
- 面向手机、平板和桌面的聊天式文件传输界面
- 流式保存文件，并使用 `.uploading` 临时文件避免半成品污染
- 文件名安全处理、路径穿越防护、同名文件自动生成可读名称
- 支持配置端口、存储目录、上传大小限制和可选访问令牌
- 前端支持 English / 简体中文轻量多语言
- 启动完成后自动使用默认浏览器打开页面
- Windows 使用 LanTransfer 专属图标和原生托盘“打开/退出”菜单并隐藏控制台；macOS/Linux 保持控制台行为

## 界面截图

![LanTransfer 桌面界面](./screenshots/lantransfer-product-preview-zh-CN.png)

## 平台支持

接收端：

- Windows、macOS、Linux
- 需要 .NET 10

发送端：

- 任意现代浏览器
- 支持 Windows、macOS、Linux、Android、iOS

网络要求：

- 默认适用于同一局域网内的设备；跨网络使用需要自行配置 VPN、内网穿透或其他路由方案。

## 快速开始

```bash
dotnet run --project src/LanTransfer.Host
```

LanTransfer 会监听 `http://0.0.0.0:8765`，并自动用默认浏览器打开本机页面。点击页面菜单中的“连接新设备”，即可显示检测到的局域网地址二维码。

打开以下地址之一：

- 当前设备：`http://localhost:8765`
- 局域网内其他设备：`http://<接收端 IP>:8765`

第一阶段默认使用局域网 HTTP，不需要信任 ASP.NET Core 开发 HTTPS 证书。

## 配置说明

LanTransfer 从 `src/LanTransfer.Host/appsettings.json` 的 `LanTransfer` 节读取配置。

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

如果配置了 `AccessToken`，受保护接口需要通过 `X-LanTransfer-Token` 请求头或 `?token=...` query 参数传递令牌。

当 `StorageDirectory` 为 `null` 时，LanTransfer 会把接收文件保存到当前用户的本地应用数据目录（Windows 为 `%LOCALAPPDATA%\LanTransfer\uploads`）。可以配置绝对路径或相对于程序目录的路径覆盖默认值。默认目录与程序文件分离，因此通过包管理器升级或卸载时不会删除用户文件。

也可以通过环境变量覆盖配置。环境变量使用 `LanTransfer__` 前缀，例如：

```bash
LanTransfer__Port=9000 dotnet run --project src/LanTransfer.Host
```

PowerShell 示例：

```powershell
$env:LanTransfer__Port = "9000"
dotnet run --project src/LanTransfer.Host
```

## API 概览

- `GET /api/health`：返回服务状态和设备名称。
- `GET /api/connect`：返回可用的局域网连接地址。
- `GET /api/connect/qr?url=...`：返回本地生成的 SVG 二维码。
- `GET /api/messages`：列出纯文字备注。
- `POST /api/messages`：发送纯文字备注。
- `POST /api/files/upload`：上传一个名为 `file` 的 multipart 文件字段。
- `GET /api/files`：获取已接收文件列表。
- `GET /api/files/{fileName}`：下载指定文件。

错误响应使用稳定的 `errorCode`，例如 `file_too_large`、`invalid_file_name`、`invalid_message`、`unauthorized`、`upload_failed`、`network_error`。

## 从源码构建

```bash
dotnet build LanTransfer.sln
dotnet test LanTransfer.sln
```

也可以直接运行构建后的可执行程序：

```bash
src/LanTransfer.Host/bin/Debug/net10.0/lantransfer
```

Windows：

```powershell
.\src\LanTransfer.Host\bin\Debug\net10.0\lantransfer.exe
```

## 发布版本

使用发布脚本可以自动测试、创建 tag 并推送版本：

```powershell
.\scripts\release.ps1 0.1.0
```

脚本会创建并推送 `v0.1.0` tag。GitHub Actions 随后会自动创建 GitHub Release，生成自包含发布包，并上传 Windows x64、Linux x64/ARM64、macOS x64/ARM64 版本。

如果只想预览检查流程、不创建 tag，可以运行：

```powershell
.\scripts\release.ps1 0.1.0 -DryRun
```

Windows Release 同时会生成 `LanTransfer-版本号-win-x64-Setup.exe` 安装程序。安装程序支持 English、简体中文和繁体中文，会根据系统语言自动选择，也可以在安装开始时手动切换语言。安装程序默认创建开始菜单快捷方式，并提供创建桌面快捷方式和登录 Windows 后自动启动的选项。应用使用自包含 .NET 发布，不需要另外安装 .NET Runtime。

安装程序使用当前用户目录，默认安装到 `%LOCALAPPDATA%\Programs\LanTransfer`，不需要管理员权限。接收文件默认保存在 `%LOCALAPPDATA%\LanTransfer\uploads`，卸载应用不会删除这些用户文件。

本地生成 Windows 安装包需要先安装 Inno Setup 6，然后运行：

```powershell
.\scripts\build-installer.ps1 -Version 0.6.0
```

## 项目结构

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

## 路线图

- 可选的限时配对码确认流程
- 已接收文件删除与重命名
- 更好的图片/文件缩略图
- 桌面系统通知
- 面向非可信网络的更强认证方案

## 许可证

MIT
