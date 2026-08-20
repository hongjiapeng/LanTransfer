using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
#if WINDOWS_UI_AUTOMATION
using UIA = Interop.UIAutomationClient;
#endif

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
            if (OperatingSystem.IsWindows() && TryActivateExistingBrowserTab())
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

#if WINDOWS_UI_AUTOMATION
    private static bool TryActivateExistingBrowserTab()
    {
        try
        {
            UIA.IUIAutomation automation = new UIA.CUIAutomation8();
            var matchingWindow = IntPtr.Zero;

            EnumWindows((window, _) =>
            {
                if (!IsWindowVisible(window) || !IsBrowserProcessWindow(window))
                {
                    return true;
                }

                if (!TrySelectLanTransferTab(automation, window))
                {
                    return true;
                }

                matchingWindow = window;
                return false;
            }, IntPtr.Zero);

            return matchingWindow != IntPtr.Zero && TryRestoreAndActivate(matchingWindow);
        }
        catch (COMException)
        {
            return false;
        }
        catch (InvalidCastException)
        {
            return false;
        }
    }

    private static bool TrySelectLanTransferTab(UIA.IUIAutomation automation, IntPtr window)
    {
        try
        {
            var browserWindow = automation.ElementFromHandle(window);
            if (browserWindow is null)
            {
                return false;
            }

            var nameCondition = automation.CreatePropertyCondition(
                UIA.UIA_PropertyIds.UIA_NamePropertyId,
                PageTitle);
            var tabCondition = automation.CreatePropertyCondition(
                UIA.UIA_PropertyIds.UIA_ControlTypePropertyId,
                UIA.UIA_ControlTypeIds.UIA_TabItemControlTypeId);
            var condition = automation.CreateAndCondition(nameCondition, tabCondition);
            var tabs = browserWindow.FindAll(UIA.TreeScope.TreeScope_Subtree, condition);

            for (var index = 0; index < tabs.Length; index++)
            {
                var tab = tabs.GetElement(index);
                if (tab is null)
                {
                    continue;
                }

                if (tab.GetCurrentPattern(UIA.UIA_PatternIds.UIA_SelectionItemPatternId)
                    is UIA.IUIAutomationSelectionItemPattern selectionItem)
                {
                    selectionItem.Select();
                    tab.SetFocus();
                    return true;
                }
            }
        }
        catch (COMException)
        {
        }
        catch (InvalidCastException)
        {
        }

        return false;
    }
#else
    private static bool TryActivateExistingBrowserTab() => false;
#endif

    private static bool TryRestoreAndActivate(IntPtr window)
    {
        if (IsIconic(window))
        {
            ShowWindow(window, SwRestore);
        }

        return SetForegroundWindow(window);
    }

    private static bool IsBrowserProcessWindow(IntPtr window)
    {
        GetWindowThreadProcessId(window, out var processId);
        return processId != 0 && IsBrowserProcess(processId);
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

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);
}
