using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
#if WINDOWS_UI_AUTOMATION
using UIA = Interop.UIAutomationClient;
#endif

namespace LanTransfer.Services;

[SupportedOSPlatform("windows")]
public static class WindowsFileRevealer
{
    public static Task<bool> RevealAsync(string fullPath, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                completion.SetResult(Reveal(fullPath, cancellationToken));
            }
            catch (OperationCanceledException)
            {
                completion.SetCanceled(cancellationToken);
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        })
        {
            IsBackground = true,
            Name = "LanTransfer reveal file"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private static bool Reveal(string fullPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var initiatingWindow = GetForegroundWindow();
        Marshal.ThrowExceptionForHR(CoInitializeEx(IntPtr.Zero, 2));
        try
        {
            var item = IntPtr.Zero;
            try
            {
                Marshal.ThrowExceptionForHR(SHParseDisplayName(fullPath, IntPtr.Zero, out item, 0, out _));
                // With no child array, the Shell opens the parent and selects this item.
                Marshal.ThrowExceptionForHR(SHOpenFolderAndSelectItems(item, 0, IntPtr.Zero, 0));
            }
            finally
            {
                Marshal.FreeCoTaskMem(item);
            }

            dynamic windows = Activator.CreateInstance(Type.GetTypeFromCLSID(
                new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39"), throwOnError: true)!)!;
            try
            {
                var directory = Path.GetDirectoryName(fullPath)!;
                var timeout = Stopwatch.StartNew();
                // Explorer may reuse a window or create one asynchronously.
                while (timeout.Elapsed < TimeSpan.FromSeconds(3))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    for (var index = 0; index < (int)windows.Count; index++)
                    {
                        object? shellWindow = null;
                        try
                        {
                            shellWindow = windows.Item(index);
                            if (shellWindow is null)
                            {
                                continue;
                            }

                            dynamic explorer = shellWindow;
                            if (string.Equals(Path.GetFileName((string)explorer.FullName), "explorer.exe",
                                    StringComparison.OrdinalIgnoreCase)
                                && Uri.TryCreate((string)explorer.LocationURL, UriKind.Absolute, out var location)
                                && location.IsFile
                                && string.Equals(Path.TrimEndingDirectorySeparator(location.LocalPath),
                                    Path.TrimEndingDirectorySeparator(directory), StringComparison.OrdinalIgnoreCase)
                                && TryActivate(new IntPtr((long)explorer.HWND), initiatingWindow))
                            {
                                return true;
                            }
                        }
                        catch (COMException)
                        {
                            // A window may close or navigate while the collection is inspected.
                        }
                        finally
                        {
                            if (shellWindow is not null)
                            {
                                Marshal.ReleaseComObject(shellWindow);
                            }
                        }
                    }

                    if (cancellationToken.WaitHandle.WaitOne(100))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
            }
            finally
            {
                Marshal.ReleaseComObject((object)windows);
            }

            return false;
        }
        finally
        {
            CoUninitialize();
        }
    }

    private static bool TryActivate(IntPtr window, IntPtr initiatingWindow)
    {
        // Windows 11 Shell entries can refer to a tab; only manipulate its top-level window.
        window = GetAncestor(window, 2 /* GA_ROOT */);
        if (window == IntPtr.Zero)
        {
            return false;
        }

        var foreground = GetForegroundWindow();
        if (foreground != IntPtr.Zero && foreground != initiatingWindow && foreground != window)
        {
            // Do not take focus back if the user switched applications while Explorer opened.
            return false;
        }

        if (IsIconic(window))
        {
            ShowWindowAsync(window, 9 /* SW_RESTORE */);
            // Restore is asynchronous. Inspect the restored bounds on the next attempt.
            return false;
        }

        if (!EnsureUsableBounds(window))
        {
            return false;
        }

        SetForegroundWindow(window);
        if (GetForegroundWindow() == window)
        {
            return true;
        }

#if WINDOWS_UI_AUTOMATION
        // The click reaches the browser, while the request runs in the background host.
        // Ask Explorer's accessibility provider to focus its own window as well.
        UIA.IUIAutomation automation = new UIA.CUIAutomation8();
        object? element = null;
        try
        {
            element = automation.ElementFromHandle(window);
            ((UIA.IUIAutomationElement)element).SetFocus();
        }
        finally
        {
            if (element is not null)
            {
                Marshal.ReleaseComObject(element);
            }
            Marshal.ReleaseComObject(automation);
        }
#endif

        return GetForegroundWindow() == window;
    }

    private static bool EnsureUsableBounds(IntPtr window)
    {
        if (IsZoomed(window))
        {
            return true;
        }

        // Explorer is DPI-aware; use physical pixels consistently for bounds and work areas.
        var previousDpiContext = SetThreadDpiAwarenessContext(new IntPtr(-4 /* PER_MONITOR_AWARE_V2 */));
        if (previousDpiContext == IntPtr.Zero)
        {
            return true;
        }

        try
        {
            var monitor = MonitorFromWindow(window, 2 /* MONITOR_DEFAULTTONEAREST */);
            var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
            if (!GetWindowRect(window, out var bounds) || !GetMonitorInfo(monitor, ref info))
            {
                return true;
            }

            var workArea = info.WorkArea;
            if (workArea.Width <= 0 || workArea.Height <= 0)
            {
                return true;
            }

            var dpi = GetDpiForWindow(window);
            var scale = dpi == 0 ? 1 : dpi / 96d;
            var visibleWidth = Math.Min(bounds.Right, workArea.Right) - Math.Max(bounds.Left, workArea.Left);
            var visibleHeight = Math.Min(bounds.Bottom, workArea.Bottom) - Math.Max(bounds.Top, workArea.Top);
            var tooSmall = bounds.Width < Math.Min(480 * scale, workArea.Width * 0.8)
                || bounds.Height < Math.Min(320 * scale, workArea.Height * 0.8);
            var offScreen = visibleWidth < Math.Min(bounds.Width, 160 * scale)
                || visibleHeight < Math.Min(bounds.Height, 80 * scale);
            if (!tooSmall && !offScreen)
            {
                return true;
            }

            // Fix only unusable placements, keeping normal and maximized windows as the user left them.
            var width = (int)Math.Min(1000 * scale, workArea.Width * 0.85);
            var height = (int)Math.Min(700 * scale, workArea.Height * 0.85);
            SetWindowPos(window, IntPtr.Zero,
                workArea.Left + (workArea.Width - width) / 2,
                workArea.Top + (workArea.Height - height) / 2,
                width, height,
                0x0004 | 0x0010 | 0x4000 /* SWP_NOZORDER | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS */);
            // Confirm the new bounds on the next attempt before reporting success.
            return false;
        }
        finally
        {
            SetThreadDpiAwarenessContext(previousDpiContext);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowRectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public uint Size;
        public WindowRectangle Monitor;
        public WindowRectangle WorkArea;
        public uint Flags;
    }

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(IntPtr reserved, uint apartment);

    [DllImport("ole32.dll")]
    private static extern void CoUninitialize();

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHParseDisplayName(string name, IntPtr bindContext, out IntPtr item,
        uint attributesIn, out uint attributesOut);

    [DllImport("shell32.dll")]
    private static extern int SHOpenFolderAndSelectItems(IntPtr item, uint count, IntPtr children, uint flags);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr window, uint flags);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr window, out WindowRectangle bounds);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter,
        int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr window, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
}
