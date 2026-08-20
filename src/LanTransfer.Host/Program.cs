using LanTransfer.Core.Abstractions;
using LanTransfer.Core.Models;
using LanTransfer.Core.Options;
using LanTransfer.Core.Services;
using LanTransfer.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QRCoder;
using System.Diagnostics;
using System.Net;

Mutex? singleInstanceMutex = null;
if (OperatingSystem.IsWindows())
{
    singleInstanceMutex = new Mutex(
        initiallyOwned: true,
        name: @"Local\LanTransfer.SingleInstance",
        createdNew: out var createdNew);

    if (!createdNew)
    {
        WindowsTrayService.TryActivateExistingInstance();
        singleInstanceMutex.Dispose();
        return;
    }
}

const long MultipartOverheadAllowanceBytes = 16L * 1024 * 1024;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
    WebRootPath = ResolveWebRootPath()
});

var startupOptions = LoadOptions(builder.Configuration);
var requestedPort = startupOptions.Port;
startupOptions.Port = PortResolver.Resolve(requestedPort);

builder.WebHost.UseUrls($"http://0.0.0.0:{startupOptions.Port}");
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = startupOptions.MaxFileSizeBytes + MultipartOverheadAllowanceBytes;
});

builder.Services.AddSingleton(Options.Create(startupOptions));
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = startupOptions.MaxFileSizeBytes + MultipartOverheadAllowanceBytes;
});

builder.Services.AddSingleton<IFileStorage>(services =>
{
    var options = services.GetRequiredService<IOptions<LanTransferOptions>>().Value;
    return new LocalFileStorage(options);
});
builder.Services.AddSingleton<IFileInbox, FileInboxService>();
builder.Services.AddSingleton<ITextMessageStore>(services =>
{
    var options = services.GetRequiredService<IOptions<LanTransferOptions>>().Value;
    return new LocalTextMessageStore(options);
});
builder.Services.AddSingleton(services =>
{
    var options = services.GetRequiredService<IOptions<LanTransferOptions>>().Value;
    return new ConnectionUrlProvider(options);
});
builder.Services.AddHostedService<WindowsTrayService>();

var app = builder.Build();
var contentTypes = new FileExtensionContentTypeProvider();

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        context.Context.Response.Headers.Pragma = "no-cache";
        context.Context.Response.Headers.Expires = "0";
    }
});

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    name = "LanTransfer",
    deviceName = Environment.MachineName
}));

app.MapGet("/api/capabilities", (HttpContext context) => Results.Ok(new
{
    canRevealFiles = OperatingSystem.IsWindows() && IsLoopbackRequest(context)
}));

app.MapGet("/api/connect", (
    HttpContext context,
    ConnectionUrlProvider connectionUrls,
    IOptions<LanTransferOptions> options) =>
{
    if (!IsAuthorized(context, options.Value))
    {
        return Results.Json(ErrorResult.Unauthorized(), statusCode: StatusCodes.Status401Unauthorized);
    }

    return Results.Ok(new
    {
        urls = connectionUrls.GetConnectionUrls(),
        localUrl = connectionUrls.LocalUrl
    });
});

app.MapGet("/api/connect/qr", (
    string url,
    HttpContext context,
    ConnectionUrlProvider connectionUrls,
    IOptions<LanTransferOptions> options) =>
{
    if (!IsAuthorized(context, options.Value))
    {
        return Results.Json(ErrorResult.Unauthorized(), statusCode: StatusCodes.Status401Unauthorized);
    }

    var allowedUrls = connectionUrls.GetConnectionUrls().Select(item => item.Url).ToHashSet(StringComparer.Ordinal);
    if (!allowedUrls.Contains(url))
    {
        return Results.BadRequest(new ErrorResult("invalid_connect_url", "Connection URL is invalid."));
    }

    using var qrData = QRCodeGenerator.GenerateQrCode(url, QRCodeGenerator.ECCLevel.M);
    using var qrCode = new SvgQRCode(qrData);
    var svg = qrCode.GetGraphic(8, "#171717", "#ffffff", drawQuietZones: true, sizingMode: SvgQRCode.SizingMode.ViewBoxAttribute);
    return Results.Text(svg, "image/svg+xml; charset=utf-8");
});

app.MapGet("/api/messages", async (
    HttpContext context,
    ITextMessageStore messages,
    IOptions<LanTransferOptions> options,
    CancellationToken cancellationToken) =>
{
    if (!IsAuthorized(context, options.Value))
    {
        return Results.Json(ErrorResult.Unauthorized(), statusCode: StatusCodes.Status401Unauthorized);
    }

    return Results.Ok(await messages.ListAsync(cancellationToken));
});

app.MapPost("/api/messages", async (
    TextMessageRequest request,
    HttpContext context,
    ITextMessageStore messages,
    IOptions<LanTransferOptions> options,
    CancellationToken cancellationToken) =>
{
    if (!IsAuthorized(context, options.Value))
    {
        return Results.Json(ErrorResult.Unauthorized(), statusCode: StatusCodes.Status401Unauthorized);
    }

    try
    {
        return Results.Ok(await messages.AddAsync(request.Text, cancellationToken));
    }
    catch (MessageValidationException)
    {
        return Results.BadRequest(ErrorResult.InvalidMessage());
    }
    catch
    {
        return Results.Json(ErrorResult.MessageFailed(), statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPost("/api/files/upload", async (
    HttpContext context,
    IFileInbox inbox,
    IOptions<LanTransferOptions> options,
    CancellationToken cancellationToken) =>
{
    if (!IsAuthorized(context, options.Value))
    {
        return Results.Json(ErrorResult.Unauthorized(), statusCode: StatusCodes.Status401Unauthorized);
    }

    if (!context.Request.HasFormContentType)
    {
        return Results.BadRequest(ErrorResult.UploadFailed());
    }

    try
    {
        var form = await context.Request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();

        if (file is null)
        {
            return Results.BadRequest(ErrorResult.UploadFailed());
        }

        await using var input = file.OpenReadStream();
        var result = await inbox.SaveAsync(file.FileName, input, file.Length, cancellationToken);
        return Results.Ok(result);
    }
    catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
    {
        return Results.Json(ErrorResult.FileTooLarge(), statusCode: StatusCodes.Status413PayloadTooLarge);
    }
    catch (InvalidDataException)
    {
        return Results.Json(ErrorResult.FileTooLarge(), statusCode: StatusCodes.Status413PayloadTooLarge);
    }
    catch (FileStorageException ex) when (ex.ErrorCode == ErrorCodes.FileTooLarge)
    {
        return Results.Json(ex.ToErrorResult(), statusCode: StatusCodes.Status413PayloadTooLarge);
    }
    catch (FileStorageException ex) when (ex.ErrorCode == ErrorCodes.InvalidFileName)
    {
        return Results.BadRequest(ex.ToErrorResult());
    }
    catch
    {
        return Results.Json(ErrorResult.UploadFailed(), statusCode: StatusCodes.Status500InternalServerError);
    }
})
.DisableAntiforgery();

app.MapGet("/api/files", async (
    HttpContext context,
    IFileInbox inbox,
    IOptions<LanTransferOptions> options,
    CancellationToken cancellationToken) =>
{
    if (!IsAuthorized(context, options.Value))
    {
        return Results.Json(ErrorResult.Unauthorized(), statusCode: StatusCodes.Status401Unauthorized);
    }

    var files = await inbox.ListAsync(cancellationToken);
    return Results.Ok(files);
});

app.MapPost("/api/files/{fileName}/reveal", async (
    string fileName,
    HttpContext context,
    IFileInbox inbox,
    IOptions<LanTransferOptions> options,
    CancellationToken cancellationToken) =>
{
    if (!IsAuthorized(context, options.Value))
    {
        return Results.Json(ErrorResult.Unauthorized(), statusCode: StatusCodes.Status401Unauthorized);
    }

    if (!OperatingSystem.IsWindows() || !IsLoopbackRequest(context))
    {
        return Results.Json(
            new ErrorResult("reveal_not_available", "File location can only be opened on the host computer."),
            statusCode: StatusCodes.Status403Forbidden);
    }

    try
    {
        var file = await inbox.GetAsync(fileName, cancellationToken);
        if (file is null)
        {
            return Results.NotFound(ErrorResult.FileNotFound());
        }

        var fullPath = Path.GetFullPath(Path.Combine(options.Value.StorageDirectory, file.FileName));
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{fullPath}\"")
        {
            UseShellExecute = true
        });
        return Results.Ok(new { revealed = true });
    }
    catch (FileStorageException ex) when (ex.ErrorCode == ErrorCodes.InvalidFileName)
    {
        return Results.BadRequest(ex.ToErrorResult());
    }
    catch
    {
        return Results.Json(
            new ErrorResult("reveal_failed", "File location could not be opened."),
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/api/files/{*fileName}", async (
    string fileName,
    HttpContext context,
    IFileInbox inbox,
    IOptions<LanTransferOptions> options,
    CancellationToken cancellationToken) =>
{
    if (!IsAuthorized(context, options.Value))
    {
        return Results.Json(ErrorResult.Unauthorized(), statusCode: StatusCodes.Status401Unauthorized);
    }

    try
    {
        var file = await inbox.GetAsync(fileName, cancellationToken);
        var stream = await inbox.OpenReadAsync(fileName, cancellationToken);
        if (file is null || stream is null)
        {
            return Results.NotFound(ErrorResult.FileNotFound());
        }

        if (!contentTypes.TryGetContentType(file.FileName, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        return Results.File(
            stream,
            contentType,
            fileDownloadName: file.FileName,
            lastModified: file.LastModifiedTime);
    }
    catch (FileStorageException ex) when (ex.ErrorCode == ErrorCodes.InvalidFileName)
    {
        return Results.BadRequest(ex.ToErrorResult());
    }
});

app.Lifetime.ApplicationStarted.Register(() =>
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("LanTransfer");
    var options = app.Services.GetRequiredService<IOptions<LanTransferOptions>>().Value;
    var connectionUrls = app.Services.GetRequiredService<ConnectionUrlProvider>();
    if (options.Port != requestedPort)
    {
        logger.LogWarning(
            "Configured port {RequestedPort} is unavailable. Using available port {ActualPort} instead.",
            requestedPort,
            options.Port);
    }

    logger.LogInformation("LanTransfer is running.");
    logger.LogInformation("Local URL: {LocalUrl}", connectionUrls.LocalUrl);
    foreach (var connectionUrl in connectionUrls.GetConnectionUrls())
    {
        logger.LogInformation("LAN URL: {LanUrl}", connectionUrl.Url);
    }

    if (options.OpenBrowserOnStart)
    {
        BrowserLauncher.TryOpen(connectionUrls.LocalUrl, logger);
    }
});

try
{
    app.Run();
}
finally
{
    singleInstanceMutex?.Dispose();
}

static bool IsAuthorized(HttpContext context, LanTransferOptions options)
{
    if (string.IsNullOrWhiteSpace(options.AccessToken))
    {
        return true;
    }

    var headerToken = context.Request.Headers["X-LanTransfer-Token"].FirstOrDefault();
    var queryToken = context.Request.Query["token"].FirstOrDefault();

    return string.Equals(headerToken, options.AccessToken, StringComparison.Ordinal) ||
        string.Equals(queryToken, options.AccessToken, StringComparison.Ordinal);
}

static bool IsLoopbackRequest(HttpContext context)
{
    var remoteAddress = context.Connection.RemoteIpAddress;
    return remoteAddress is not null && IPAddress.IsLoopback(remoteAddress);
}

static LanTransferOptions LoadOptions(IConfiguration configuration)
{
    var section = configuration.GetSection("LanTransfer");
    return new LanTransferOptions
    {
        Port = ReadInt(section, "Port", 8765),
        StorageDirectory = StoragePathResolver.Resolve(section["StorageDirectory"], AppContext.BaseDirectory),
        MaxFileSizeBytes = ReadLong(section, "MaxFileSizeBytes", 1024L * 1024 * 1024),
        MaxMessageLength = ReadInt(section, "MaxMessageLength", 4000),
        OpenBrowserOnStart = ReadBool(section, "OpenBrowserOnStart", true),
        EnableWindowsTray = ReadBool(section, "EnableWindowsTray", true),
        AccessToken = section["AccessToken"]
    };
}

static int ReadInt(IConfiguration configuration, string key, int defaultValue)
{
    return int.TryParse(configuration[key], out var value) ? value : defaultValue;
}

static long ReadLong(IConfiguration configuration, string key, long defaultValue)
{
    return long.TryParse(configuration[key], out var value) ? value : defaultValue;
}

static bool ReadBool(IConfiguration configuration, string key, bool defaultValue)
{
    return bool.TryParse(configuration[key], out var value) ? value : defaultValue;
}

static string ResolveWebRootPath()
{
    var candidates = new[]
    {
        Path.Combine(AppContext.BaseDirectory, "wwwroot"),
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"),
        Path.Combine(Directory.GetCurrentDirectory(), "src", "LanTransfer.Host", "wwwroot")
    };

    return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
}

public sealed record TextMessageRequest(string Text);
