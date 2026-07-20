using LanTransfer.Core.Services;
using Xunit;

namespace LanTransfer.Tests;

public sealed class StoragePathResolverTests
{
    [Fact]
    public void Resolve_UsesLocalApplicationDataWhenUnconfigured()
    {
        var baseDirectory = Path.Combine(Path.GetTempPath(), "LanTransfer", "app");
        var localData = Path.Combine(Path.GetTempPath(), "LanTransfer", "local-data");

        var result = StoragePathResolver.Resolve(null, baseDirectory, localData);

        Assert.Equal(Path.Combine(localData, "LanTransfer", "uploads"), result);
        Assert.False(result.StartsWith(baseDirectory, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Resolve_KeepsExplicitRelativePathAppRelative()
    {
        var baseDirectory = Path.Combine(Path.GetTempPath(), "LanTransfer", "app");

        var result = StoragePathResolver.Resolve("received", baseDirectory, "ignored");

        Assert.Equal(Path.Combine(baseDirectory, "received"), result);
    }

    [Fact]
    public void Resolve_PreservesExplicitAbsolutePath()
    {
        var baseDirectory = Path.Combine(Path.GetTempPath(), "LanTransfer", "app");
        var configured = Path.Combine(Path.GetTempPath(), "LanTransfer", "custom");

        var result = StoragePathResolver.Resolve(configured, baseDirectory, "ignored");

        Assert.Equal(Path.GetFullPath(configured), result);
    }
}
