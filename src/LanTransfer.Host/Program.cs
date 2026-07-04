using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using LanTransfer.Core.Abstractions;
using LanTransfer.Core.Models;
using LanTransfer.Core.Options;
using LanTransfer.Core.Services;
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

const long MultipartOverheadAllowanceBytes = 16L * 1024 * 1024;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
    WebRootPath = ResolveWebRootPath()
});

var startupOptions = LoadOptions(builder.Configuration);

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

var app = builder.Build();
var contentTypes = new FileExtensionContentTypeProvider();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    name = "LanTransfer",
    deviceName = Environment.MachineName
}));

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
    var port = app.Services.GetRequiredService<IOptions<LanTransferOptions>>().Value.Port;
    logger.LogInformation("LanTransfer is running.");
    logger.LogInformation("Local URL: http://localhost:{Port}", port);
    logger.LogInformation("LAN URL: http://{Address}:{Port}", GetLocalIPv4Address(), port);
});

app.Run();

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

static string GetLocalIPv4Address()
{
    try
    {
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            var address = networkInterface
                .GetIPProperties()
                .UnicastAddresses
                .FirstOrDefault(item => item.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(item.Address))
                ?.Address;

            if (address is not null)
            {
                return address.ToString();
            }
        }
    }
    catch
    {
        return "127.0.0.1";
    }

    return "127.0.0.1";
}

static LanTransferOptions LoadOptions(IConfiguration configuration)
{
    var section = configuration.GetSection("LanTransfer");
    return new LanTransferOptions
    {
        Port = ReadInt(section, "Port", 8765),
        StorageDirectory = section["StorageDirectory"] ?? "uploads",
        MaxFileSizeBytes = ReadLong(section, "MaxFileSizeBytes", 1024L * 1024 * 1024),
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
