# FileTransferAssistant

[English](README.md)

FileTransferAssistant 是一个基于 .NET 和浏览器界面的跨平台局域网文件传输助手。

你只需要在一台设备上启动接收服务，然后用同一局域网内的手机、平板或电脑打开终端里显示的地址，就可以通过网页上传、下载和管理文件。

## 功能特性

* 基于浏览器的文件上传界面，支持手机、平板和桌面电脑
* 基于 .NET 10 与 ASP.NET Core Kestrel 的跨平台接收端
* 上传文件采用流式保存，不会先完整缓存到内存中
* 提供文件收件箱 API，可查看已接收文件并生成下载链接
* 支持配置端口、存储目录、上传大小限制和可选访问令牌
* 提供轻量 CLI 示例，方便本地测试和开源演示

## 平台支持

接收端：

* Windows、macOS、Linux
* 需要安装 .NET 10

发送端：

* 任意现代浏览器
* 支持 Windows、macOS、Linux、Android、iOS

网络要求：

* 默认适用于同一局域网内的设备
* 跨网络使用时，需要自行配置 VPN、内网穿透或其他网络路由方案

## 快速开始

```bash
dotnet run --project src/FileTransferAssistant.Sample
```

启动后，在同一局域网内的另一台设备上打开终端中打印出来的 URL，即可进入文件传输页面。

## 作为类库使用

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

## 配置示例

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

配置说明：

* `Port`：服务监听端口
* `DeviceName`：设备名称，用于状态展示
* `StorageDirectory`：接收文件的保存目录
* `AllowFileUploads`：是否允许上传文件
* `MaxFileSize`：单个上传文件大小限制
* `RequestHeadersTimeout`：请求头超时时间
* `RequireAccessToken`：是否要求访问令牌
* `AccessToken`：访问令牌内容

## HTTP API

* `GET /api/status`
  返回设备名称、访问 URL、上传大小限制和存储目录。

* `POST /api/upload`
  接收 `multipart/form-data` 格式的文件上传请求。

* `GET /api/files`
  获取已接收文件列表。

* `GET /api/files/{id}/download`
  下载指定的已接收文件。

* `POST /api/send`
  保留为可选的消息扩展入口。

## 项目结构

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

## 构建与测试

```bash
dotnet build FileTransferAssistant.sln
dotnet test FileTransferAssistant.sln
```

## 安全说明

默认情况下，服务会监听所有网络接口，方便同一局域网内的其他设备访问。

这对家庭或可信办公网络很方便，但在公共网络、共享网络或跨公网访问时需要额外注意安全。

建议：

* 在非可信网络中使用前，开启 `RequireAccessToken` 并设置 `AccessToken`
* 将接收文件保存到受控目录，避免污染系统关键路径
* 不要将服务端口直接暴露到公网
* 公网访问场景下，应额外增加更强的身份认证与传输加密

## 路线图

* 局域网设备发现
* 配对码流程
* 面向 PC 到 PC 的主动发送模式
* 收件箱文件删除与重命名
* 桌面托盘应用与系统通知

## License

MIT
