using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace LanTransfer.Services;

[SupportedOSPlatform("windows")]
internal static class WindowsStartupService
{
    private static string ShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup), "LanTransfer.lnk");

    public static bool IsEnabled() => File.Exists(ShortcutPath);

    public static void SetEnabled(bool enabled)
    {
        if (!enabled)
        {
            File.Delete(ShortcutPath);
            return;
        }

        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("The application executable could not be located.");
        var arguments = string.Empty;
        if (string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var assembly = Assembly.GetEntryAssembly()?.Location;
            if (string.IsNullOrEmpty(assembly))
            {
                throw new InvalidOperationException("The application assembly could not be located.");
            }
            arguments = $"\"{assembly}\"";
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ShortcutPath)!);
        var shellType = Type.GetTypeFromProgID("WScript.Shell", throwOnError: true)!;
        dynamic shell = Activator.CreateInstance(shellType)!;
        object? shortcutObject = null;
        try
        {
            shortcutObject = shell.CreateShortcut(ShortcutPath);
            dynamic shortcut = shortcutObject;
            shortcut.TargetPath = executable;
            shortcut.Arguments = arguments;
            shortcut.WorkingDirectory = AppContext.BaseDirectory;
            shortcut.Save();
        }
        finally
        {
            if (shortcutObject is not null) Marshal.FinalReleaseComObject(shortcutObject);
            Marshal.FinalReleaseComObject(shell);
        }
    }
}
