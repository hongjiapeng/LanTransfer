# LanTransfer

[English](./README.md)

LanTransfer 是一个跨平台局域网文件传输工具。你可以在一台设备上启动接收端服务，然后在同一局域网内通过手机、平板或电脑浏览器访问页面，快速上传和下载文件。

在同一局域网内，在手机、平板和电脑之间快速传输文件。

## 功能特性

- 基于浏览器的局域网上传和下载
- 基于 ASP.NET Core Kestrel 的接收端服务
- 面向手机、平板和桌面的聊天式文件传输界面
- 流式保存文件，并使用 `.uploading` 临时文件避免半成品污染
- 文件名安全处理、路径穿越防护、同名文件自动生成可读名称
- 支持配置端口、存储目录、上传大小限制和可选访问令牌
- 前端支持 English / 简体中文轻量多语言

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

启动后，在同一局域网内的另一台设备上打开 `http://<接收端 IP>:8765`。

## 配置说明

LanTransfer 从 `src/LanTransfer.Host/appsettings.json` 的 `LanTransfer` 节读取配置。

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

如果配置了 `AccessToken`，受保护接口需要通过 `X-LanTransfer-Token` 请求头或 `?token=...` query 参数传递令牌。

## API 概览

- `GET /api/health`：返回服务状态和设备名称。
- `POST /api/files/upload`：上传一个名为 `file` 的 multipart 文件字段。
- `GET /api/files`：获取已接收文件列表。
- `GET /api/files/{fileName}`：下载指定文件。

错误响应使用稳定的 `errorCode`，例如 `file_too_large`、`file_not_found`、`invalid_file_name`、`unauthorized`、`upload_failed`、`network_error`。

## 从源码构建

```bash
dotnet build LanTransfer.sln
dotnet test LanTransfer.sln
```

## 项目结构

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

## 路线图

- 配对码流程
- 用二维码打开局域网访问地址
- 已接收文件删除与重命名
- 桌面托盘应用与系统通知
- 面向非可信网络的更强认证方案

## 许可证

MIT
