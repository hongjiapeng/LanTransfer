using LanTransfer.Core.Abstractions;
using LanTransfer.Core.Options;
using LanTransfer.Core.Services;
using Xunit;

namespace LanTransfer.Tests;

public sealed class LocalTextMessageStoreTests : IDisposable
{
    private readonly string _storageDirectory;
    private readonly LocalTextMessageStore _store;

    public LocalTextMessageStoreTests()
    {
        _storageDirectory = Path.Combine(Path.GetTempPath(), "LanTransfer.Tests", Guid.NewGuid().ToString("N"));
        _store = new LocalTextMessageStore(new LanTransferOptions
        {
            StorageDirectory = _storageDirectory,
            MaxMessageLength = 20
        });
    }

    [Fact]
    public async Task AddAsync_TrimsAndPersistsMessage()
    {
        var added = await _store.AddAsync("  hello\nworld  ");

        var messages = await _store.ListAsync();

        var stored = Assert.Single(messages);
        Assert.Equal(added.Id, stored.Id);
        Assert.Equal("hello\nworld", stored.Text);
        Assert.True(File.Exists(Path.Combine(_storageDirectory, ".lantransfer", "messages.json")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123456789012345678901")]
    public async Task AddAsync_RejectsEmptyOrOversizedMessage(string text)
    {
        await Assert.ThrowsAsync<MessageValidationException>(() => _store.AddAsync(text));
        Assert.Empty(await _store.ListAsync());
    }

    [Fact]
    public async Task AddAsync_SerializesConcurrentWritesWithoutDataLoss()
    {
        var writes = Enumerable.Range(0, 20)
            .Select(index => _store.AddAsync($"message {index}"));

        await Task.WhenAll(writes);

        var messages = await _store.ListAsync();
        Assert.Equal(20, messages.Count);
        Assert.Equal(20, messages.Select(message => message.Id).Distinct().Count());
        Assert.True(messages.SequenceEqual(messages.OrderByDescending(message => message.CreatedAt)));
    }

    [Fact]
    public async Task MessageState_DoesNotAppearInFileStorageListing()
    {
        await _store.AddAsync("hello");
        var files = new LocalFileStorage(new LanTransferOptions
        {
            StorageDirectory = _storageDirectory
        });

        Assert.Empty(await files.ListAsync());
    }

    [Fact]
    public void TextMessageStoreContract_ExposesDeleteAsyncReturningBooleanTask()
    {
        var method = typeof(ITextMessageStore).GetMethod(
            nameof(ITextMessageStore.DeleteAsync),
            new[] { typeof(Guid), typeof(CancellationToken) });

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<bool>), method.ReturnType);
    }

    [Fact]
    public async Task DeleteAsync_ExistingMessage_RemovesOnlyTargetAndReturnsTrue()
    {
        var target = await _store.AddAsync("target");
        var firstControl = await _store.AddAsync("keep one");
        var secondControl = await _store.AddAsync("keep two");

        var deleted = await _store.DeleteAsync(target.Id);

        Assert.True(deleted);
        var messages = await _store.ListAsync();
        Assert.Equal(2, messages.Count);
        Assert.DoesNotContain(messages, message => message.Id == target.Id);
        Assert.Contains(messages, message => message.Id == firstControl.Id && message.Text == "keep one");
        Assert.Contains(messages, message => message.Id == secondControl.Id && message.Text == "keep two");
    }

    [Fact]
    public async Task DeleteAsync_ExistingMessage_PersistsRemoval()
    {
        var persistenceDirectory = Path.Combine(_storageDirectory, "persistence");
        var options = new LanTransferOptions
        {
            StorageDirectory = persistenceDirectory,
            MaxMessageLength = 20
        };
        Guid retainedId;

        using (var writer = new LocalTextMessageStore(options))
        {
            var target = await writer.AddAsync("remove me");
            retainedId = (await writer.AddAsync("keep me")).Id;
            Assert.True(await writer.DeleteAsync(target.Id));
        }

        using var reopened = new LocalTextMessageStore(options);
        var persisted = Assert.Single(await reopened.ListAsync());
        Assert.Equal(retainedId, persisted.Id);
        Assert.Equal("keep me", persisted.Text);
    }

    [Fact]
    public async Task DeleteAsync_MissingMessage_ReturnsFalseAndPreservesAllRecords()
    {
        await _store.AddAsync("first");
        await _store.AddAsync("second");
        var before = await _store.ListAsync();

        var deleted = await _store.DeleteAsync(Guid.NewGuid());

        Assert.False(deleted);
        var after = await _store.ListAsync();
        Assert.Equal(before.Count, after.Count);
        Assert.Equal(before.Select(message => message.Id), after.Select(message => message.Id));
        Assert.Equal(before.Select(message => message.Text), after.Select(message => message.Text));
    }

    [Fact]
    public async Task DeleteAsync_CanceledToken_ThrowsWithoutCreatingState()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _store.DeleteAsync(Guid.NewGuid(), cancellation.Token));

        Assert.False(Directory.Exists(Path.Combine(_storageDirectory, ".lantransfer")));
    }

    public void Dispose()
    {
        _store.Dispose();
        if (Directory.Exists(_storageDirectory))
        {
            Directory.Delete(_storageDirectory, recursive: true);
        }
    }
}
