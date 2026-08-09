using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace LanTransfer.Services;

public static class BrowserLauncher
{
    private const string PageTitle = "LanTransfer";
    private const int SwRestore = 9;
    private static readonly HashSet<string> BrowserProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "arc",
        "brave",
        "chrome",
        "chromium",
        "firefox",
        "iexplore",
        "msedge",
        "opera",
        "vivaldi",
        "zen"
    };

    public static bool TryOpen(string url, ILogger logger)
    {
        try
        {
            if (OperatingSystem.IsWindows() && TryActivateExistingBrowserWindow())
            {
                return true;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unable to open the default browser at {Url}.", url);
            return false;
        }
    }

    private static bool TryActivateExistingBrowserWindow()
    {
        IntPtr matchingWindow = IntPtr.Zero;

        EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window))
            {
                return true;
            }

            GetWindowThreadProcessId(window, out var processId);
            if (processId == 0 || !IsBrowserProcess(processId))
            {
                return true;
            }

            var titleLength = GetWindowTextLength(window);
            if (titleLength == 0)
            {
                return true;
            }

            var title = new StringBuilder(titleLength + 1);
            GetWindowText(window, title, title.Capacity);
            if (title.ToString().Contains(PageTitle, StringComparison.OrdinalIgnoreCase))
            {
                matchingWindow = window;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        if (matchingWindow == IntPtr.Zero)
        {
            return false;
        }

        if (IsIconic(matchingWindow))
        {
            ShowWindow(matchingWindow, SwRestore);
        }

        return SetForegroundWindow(matchingWindow);
    }

    private static bool IsBrowserProcess(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return BrowserProcessNames.Contains(process.ProcessName);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);
}
