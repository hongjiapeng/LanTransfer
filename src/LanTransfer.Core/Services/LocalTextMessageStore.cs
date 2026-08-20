using System.Text.Json;
using LanTransfer.Core.Abstractions;
using LanTransfer.Core.Models;
using LanTransfer.Core.Options;

namespace LanTransfer.Core.Services;

public sealed class LocalTextMessageStore : ITextMessageStore, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _stateDirectory;
    private readonly string _messagesPath;
    private readonly int _maxMessageLength;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LocalTextMessageStore(LanTransferOptions options)
    {
        var storageRoot = Path.GetFullPath(options.StorageDirectory);
        _stateDirectory = Path.Combine(storageRoot, ".lantransfer");
        _messagesPath = Path.Combine(_stateDirectory, "messages.json");
        _maxMessageLength = options.MaxMessageLength;
    }

    public async Task<TextMessage> AddAsync(string text, CancellationToken cancellationToken = default)
    {
        var normalized = (text ?? string.Empty).Trim();
        if (normalized.Length == 0 || normalized.Length > _maxMessageLength)
        {
            throw new MessageValidationException("Message is empty or too long.");
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var messages = await ReadUnsafeAsync(cancellationToken);
            var message = new TextMessage(Guid.NewGuid(), normalized, DateTimeOffset.UtcNow);
            messages.Add(message);
            await WriteUnsafeAsync(messages, cancellationToken);
            return message;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<TextMessage>> ListAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return (await ReadUnsafeAsync(cancellationToken))
                .OrderByDescending(message => message.CreatedAt)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var messages = await ReadUnsafeAsync(cancellationToken);
            var removed = messages.RemoveAll(message => message.Id == id) > 0;
            if (!removed)
            {
                return false;
            }

            await WriteUnsafeAsync(messages, cancellationToken);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _gate.Dispose();
    }

    private async Task<List<TextMessage>> ReadUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_messagesPath))
        {
            return [];
        }

        await using var stream = new FileStream(
            _messagesPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return await JsonSerializer.DeserializeAsync<List<TextMessage>>(stream, JsonOptions, cancellationToken) ?? [];
    }

    private async Task WriteUnsafeAsync(List<TextMessage> messages, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_stateDirectory);
        var tempPath = Path.Combine(_stateDirectory, $".{Guid.NewGuid():N}.messages.tmp");

        try
        {
            await using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, messages, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(tempPath, _messagesPath, overwrite: true);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Cleanup is best effort after a failed atomic write.
        }
    }
}
