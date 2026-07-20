namespace LanTransfer.Core.Services;

public static class StoragePathResolver
{
    public static string Resolve(
        string? configuredPath,
        string baseDirectory,
        string? localApplicationData = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            var localData = localApplicationData ??
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            return string.IsNullOrWhiteSpace(localData)
                ? Path.GetFullPath(Path.Combine(baseDirectory, "uploads"))
                : Path.GetFullPath(Path.Combine(localData, "LanTransfer", "uploads"));
        }

        return Path.IsPathFullyQualified(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : Path.GetFullPath(Path.Combine(baseDirectory, configuredPath));
    }
}
