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

    public void Dispose()
    {
        _store.Dispose();
        if (Directory.Exists(_storageDirectory))
        {
            Directory.Delete(_storageDirectory, recursive: true);
        }
    }
}
