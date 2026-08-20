using LanTransfer.Core.Abstractions;
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
    public void FileStorageContracts_ExposeDeleteAsyncReturningBooleanTask()
    {
        var parameterTypes = new[] { typeof(string), typeof(CancellationToken) };

        var storageMethod = typeof(IFileStorage).GetMethod(nameof(IFileStorage.DeleteAsync), parameterTypes);
        var inboxMethod = typeof(IFileInbox).GetMethod(nameof(IFileInbox.DeleteAsync), parameterTypes);

        Assert.NotNull(storageMethod);
        Assert.Equal(typeof(Task<bool>), storageMethod.ReturnType);
        Assert.NotNull(inboxMethod);
        Assert.Equal(typeof(Task<bool>), inboxMethod.ReturnType);
    }

    [Fact]
    public async Task DeleteAsync_ExistingFile_MovesItToTrashAndReturnsTrue()
    {
        await using var stream = ToStream("payload");
        await _storage.SaveAsync("notes.txt", stream, stream.Length);

        var deleted = await _storage.DeleteAsync("notes.txt");

        Assert.True(deleted);
        Assert.False(File.Exists(Path.Combine(_storageDirectory, "notes.txt")));
        Assert.Empty(await _storage.ListAsync());
        Assert.Null(await _storage.GetAsync("notes.txt"));
        Assert.Null(await _storage.OpenReadAsync("notes.txt"));

        var trashFiles = Directory.EnumerateFiles(
            Path.Combine(_storageDirectory, ".lantransfer", "trash"),
            "*",
            SearchOption.AllDirectories).ToList();
        var trashFile = Assert.Single(trashFiles);
        Assert.Equal("notes.txt", Path.GetFileName(trashFile));
        Assert.Equal("payload", await File.ReadAllTextAsync(trashFile));
    }

    [Fact]
    public async Task DeleteAsync_MissingFile_ReturnsFalseAndPreservesExistingFiles()
    {
        await using var stream = ToStream("keep me");
        await _storage.SaveAsync("keep.txt", stream, stream.Length);

        var deleted = await _storage.DeleteAsync("missing.txt");

        Assert.False(deleted);
        var retained = Assert.Single(await _storage.ListAsync());
        Assert.Equal("keep.txt", retained.FileName);
        Assert.NotNull(await _storage.GetAsync("keep.txt"));
        await using var retainedStream = Assert.IsAssignableFrom<Stream>(await _storage.OpenReadAsync("keep.txt"));
        using var reader = new StreamReader(retainedStream);
        Assert.Equal("keep me", await reader.ReadToEndAsync());
        Assert.False(Directory.Exists(Path.Combine(_storageDirectory, ".lantransfer", "trash")));
    }

    [Fact]
    public async Task DeleteAsync_CanceledToken_ThrowsWithoutMovingFile()
    {
        await using var stream = ToStream("keep me");
        await _storage.SaveAsync("keep.txt", stream, stream.Length);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _storage.DeleteAsync("keep.txt", cancellation.Token));

        Assert.True(File.Exists(Path.Combine(_storageDirectory, "keep.txt")));
        Assert.False(Directory.Exists(Path.Combine(_storageDirectory, ".lantransfer", "trash")));
    }

    [Theory]
    [InlineData("../{0}.outside.txt")]
    [InlineData("..\\{0}.outside.txt")]
    [InlineData("%2E%2E%2F{0}.outside.txt")]
    public async Task DeleteAsync_PathLikeName_RejectsWithoutMovingOutsideFile(string pathTemplate)
    {
        var storageName = Path.GetFileName(_storageDirectory);
        var outsidePath = Path.Combine(
            Directory.GetParent(_storageDirectory)!.FullName,
            $"{storageName}.outside.txt");
        await File.WriteAllTextAsync(outsidePath, "outside");

        try
        {
            var exception = await Assert.ThrowsAsync<FileStorageException>(() =>
                _storage.DeleteAsync(string.Format(pathTemplate, storageName)));

            Assert.Equal(ErrorCodes.InvalidFileName, exception.ErrorCode);
            Assert.True(File.Exists(outsidePath));
            Assert.Equal("outside", await File.ReadAllTextAsync(outsidePath));
            Assert.False(Directory.Exists(Path.Combine(_storageDirectory, ".lantransfer", "trash")));
        }
        finally
        {
            File.Delete(outsidePath);
        }
    }

    [Fact]
    public async Task DeleteAsync_ReusedFileName_PreservesBothTrashEntries()
    {
        await using var first = ToStream("first");
        await _storage.SaveAsync("repeat.txt", first, first.Length);
        var firstDeleted = await _storage.DeleteAsync("repeat.txt");

        await using var second = ToStream("second");
        await _storage.SaveAsync("repeat.txt", second, second.Length);
        var secondDeleted = await _storage.DeleteAsync("repeat.txt");

        Assert.True(firstDeleted);
        Assert.True(secondDeleted);
        var trashFiles = Directory.EnumerateFiles(
            Path.Combine(_storageDirectory, ".lantransfer", "trash"),
            "repeat.txt",
            SearchOption.AllDirectories).ToList();
        Assert.Equal(2, trashFiles.Count);
        var contents = await Task.WhenAll(trashFiles.Select(path => File.ReadAllTextAsync(path)));
        Assert.Equal(new[] { "first", "second" }, contents.Order(StringComparer.Ordinal));
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
