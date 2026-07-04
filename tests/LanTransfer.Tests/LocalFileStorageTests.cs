using LanTransfer.Core.Models;
using LanTransfer.Core.Options;
using LanTransfer.Core.Services;
using Xunit;

namespace LanTransfer.Tests;

public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _storageDirectory;
    private readonly LocalFileStorage _storage;

    public LocalFileStorageTests()
    {
        _storageDirectory = Path.Combine(Path.GetTempPath(), "LanTransfer.Tests", Guid.NewGuid().ToString("N"));
        _storage = new LocalFileStorage(new LanTransferOptions
        {
            StorageDirectory = _storageDirectory,
            MaxFileSizeBytes = 1024
        });
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("..\\evil.txt")]
    [InlineData("folder/file.txt")]
    public async Task SaveAsync_RejectsPathTraversalNames(string fileName)
    {
        await using var stream = ToStream("payload");

        var exception = await Assert.ThrowsAsync<FileStorageException>(() =>
            _storage.SaveAsync(fileName, stream, stream.Length));

        Assert.Equal(ErrorCodes.InvalidFileName, exception.ErrorCode);
        Assert.False(File.Exists(Path.Combine(_storageDirectory, "evil.txt")));
    }

    [Fact]
    public async Task SaveAsync_RejectsAbsolutePathNames()
    {
        await using var stream = ToStream("payload");
        var absolutePath = Path.Combine(Path.GetPathRoot(_storageDirectory)!, "evil.txt");

        var exception = await Assert.ThrowsAsync<FileStorageException>(() =>
            _storage.SaveAsync(absolutePath, stream, stream.Length));

        Assert.Equal(ErrorCodes.InvalidFileName, exception.ErrorCode);
    }

    [Fact]
    public async Task SaveAsync_ReplacesInvalidFileNameCharacters()
    {
        await using var stream = ToStream("payload");

        var result = await _storage.SaveAsync("bad:name.txt", stream, stream.Length);

        Assert.Equal("bad_name.txt", result.FileName);
        Assert.True(File.Exists(Path.Combine(_storageDirectory, "bad_name.txt")));
    }

    [Fact]
    public async Task SaveAsync_DoesNotAllowFinalPathToEscapeStorageDirectory()
    {
        await using var stream = ToStream("payload");

        var exception = await Assert.ThrowsAsync<FileStorageException>(() =>
            _storage.SaveAsync("%2E%2E%2Fevil.txt", stream, stream.Length));

        Assert.Equal(ErrorCodes.InvalidFileName, exception.ErrorCode);
        Assert.False(File.Exists(Path.Combine(_storageDirectory, "..", "evil.txt")));
    }

    [Fact]
    public async Task ListAsync_ReturnsSavedFiles()
    {
        await using var stream = ToStream("payload");
        await _storage.SaveAsync("notes.txt", stream, stream.Length);

        var files = await _storage.ListAsync();

        var file = Assert.Single(files);
        Assert.Equal("notes.txt", file.FileName);
        Assert.Equal(7, file.Size);
        Assert.Equal("/api/files/notes.txt", file.DownloadUrl);
    }

    [Fact]
    public async Task SaveAsync_EnforcesUploadSizeLimit()
    {
        await using var stream = ToStream(new string('a', 2048));

        var exception = await Assert.ThrowsAsync<FileStorageException>(() =>
            _storage.SaveAsync("large.txt", stream, stream.Length));

        Assert.Equal(ErrorCodes.FileTooLarge, exception.ErrorCode);
        Assert.Empty(Directory.EnumerateFiles(_storageDirectory));
    }

    [Fact]
    public async Task GetAsync_ReturnsNullForMissingFile()
    {
        var file = await _storage.GetAsync("missing.txt");

        Assert.Null(file);
    }

    [Fact]
    public async Task SaveAsync_GeneratesReadableNameForDuplicates()
    {
        await using var first = ToStream("one");
        await using var second = ToStream("two");

        var firstResult = await _storage.SaveAsync("photo.jpg", first, first.Length);
        var secondResult = await _storage.SaveAsync("photo.jpg", second, second.Length);

        Assert.Equal("photo.jpg", firstResult.FileName);
        Assert.Equal("photo (1).jpg", secondResult.FileName);
        Assert.True(File.Exists(Path.Combine(_storageDirectory, "photo (1).jpg")));
    }

    [Fact]
    public void CoreProject_DoesNotReferenceAspNetCore()
    {
        var repoRoot = FindRepositoryRoot();
        var projectFile = Path.Combine(repoRoot, "src", "LanTransfer.Core", "LanTransfer.Core.csproj");
        var projectXml = File.ReadAllText(projectFile);

        Assert.DoesNotContain("Microsoft.AspNetCore", projectXml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FrameworkReference", projectXml, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_storageDirectory))
        {
            Directory.Delete(_storageDirectory, recursive: true);
        }
    }

    private static MemoryStream ToStream(string text)
    {
        return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "LanTransfer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
