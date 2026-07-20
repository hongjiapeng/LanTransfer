using System.ComponentModel;
using System.Runtime.InteropServices;
using LanTransfer.Core.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LanTransfer.Services;

public sealed class WindowsTrayService : IHostedService, IDisposable
{
    private const int WmApp = 0x8000;
    private const int WmClose = 0x0010;
    private const int WmDestroy = 0x0002;
    private const int WmLButtonUp = 0x0202;
    private const int WmLButtonDblClk = 0x0203;
    private const int WmRButtonUp = 0x0205;
    private const int TrayCallbackMessage = WmApp + 1;
    private const uint NimAdd = 0x00000000;
    private const uint NimDelete = 0x00000002;
    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const uint MfString = 0x00000000;
    private const uint MfSeparator = 0x00000800;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCmd = 0x0100;
    private const uint OpenCommand = 1;
    private const uint ExitCommand = 2;
    private static readonly IntPtr HwndMessage = new(-3);
    private static readonly IntPtr IdiApplication = new(32512);

    private readonly IHostApplicationLifetime _lifetime;
    private readonly ConnectionUrlProvider _urls;
    private readonly ILogger<WindowsTrayService> _logger;
    private readonly bool _enabled;
    private Thread? _thread;
    private IntPtr _windowHandle;
    private WindowProcedure? _windowProcedure;
    private bool _disposed;

    public WindowsTrayService(
        IHostApplicationLifetime lifetime,
        ConnectionUrlProvider urls,
        IOptions<LanTransferOptions> options,
        ILogger<WindowsTrayService> logger)
    {
        _lifetime = lifetime;
        _urls = urls;
        _logger = logger;
        _enabled = options.Value.EnableWindowsTray && OperatingSystem.IsWindows();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows() || !_enabled)
        {
            return Task.CompletedTask;
        }

        _thread = new Thread(RunMessageLoop)
        {
            IsBackground = true,
            Name = "LanTransfer Windows tray"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_windowHandle != IntPtr.Zero)
        {
            PostMessage(_windowHandle, WmClose, IntPtr.Zero, IntPtr.Zero);
        }

        if (_thread is { IsAlive: true })
        {
            _thread.Join(TimeSpan.FromSeconds(2));
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_windowHandle != IntPtr.Zero)
        {
            PostMessage(_windowHandle, WmClose, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private void RunMessageLoop()
    {
        try
        {
            var instance = GetModuleHandle(null);
            _windowProcedure = HandleWindowMessage;
            var className = $"LanTransferTrayWindow-{Environment.ProcessId}";
            var windowClass = new WindowClass
            {
                WindowProcedure = _windowProcedure,
                Instance = instance,
                ClassName = className
            };

            if (RegisterClass(ref windowClass) == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register the tray window class.");
            }

            _windowHandle = CreateWindowEx(
                0,
                className,
                "LanTransfer",
                0,
                0,
                0,
                0,
                0,
                HwndMessage,
                IntPtr.Zero,
                instance,
                IntPtr.Zero);

            if (_windowHandle == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the tray window.");
            }

            var iconData = CreateIconData(_windowHandle);
            if (!ShellNotifyIcon(NimAdd, ref iconData))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not add the tray icon.");
            }

            while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref message);
                DispatchMessage(ref message);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Windows tray integration is unavailable; LanTransfer will keep running.");
        }
        finally
        {
            RemoveTrayIcon();
            _windowHandle = IntPtr.Zero;
        }
    }

    private IntPtr HandleWindowMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == TrayCallbackMessage)
        {
            switch (unchecked((int)lParam.ToInt64()))
            {
                case WmLButtonUp:
                case WmLButtonDblClk:
                    BrowserLauncher.TryOpen(_urls.LocalUrl, _logger);
                    return IntPtr.Zero;
                case WmRButtonUp:
                    ShowContextMenu(window);
                    return IntPtr.Zero;
            }
        }
        else if (message == WmClose)
        {
            DestroyWindow(window);
            return IntPtr.Zero;
        }
        else if (message == WmDestroy)
        {
            RemoveTrayIcon();
            PostQuitMessage(0);
            return IntPtr.Zero;
        }

        return DefWindowProc(window, message, wParam, lParam);
    }

    private void ShowContextMenu(IntPtr window)
    {
        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var isChinese = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            AppendMenu(menu, MfString, OpenCommand, isChinese ? "打开 LanTransfer" : "Open LanTransfer");
            AppendMenu(menu, MfSeparator, 0, null);
            AppendMenu(menu, MfString, ExitCommand, isChinese ? "退出" : "Exit");
            GetCursorPos(out var point);
            SetForegroundWindow(window);
            var command = TrackPopupMenu(menu, TpmRightButton | TpmReturnCmd, point.X, point.Y, 0, window, IntPtr.Zero);
            if (command == OpenCommand)
            {
                BrowserLauncher.TryOpen(_urls.LocalUrl, _logger);
            }
            else if (command == ExitCommand)
            {
                _lifetime.StopApplication();
            }
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    private void RemoveTrayIcon()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var iconData = CreateIconData(_windowHandle);
            ShellNotifyIcon(NimDelete, ref iconData);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Could not remove the Windows tray icon during shutdown.");
        }
    }

    private static NotifyIconData CreateIconData(IntPtr window)
    {
        return new NotifyIconData
        {
            Size = (uint)Marshal.SizeOf<NotifyIconData>(),
            Window = window,
            Id = 1,
            Flags = NifMessage | NifIcon | NifTip,
            CallbackMessage = TrayCallbackMessage,
            Icon = LoadIcon(IntPtr.Zero, IdiApplication),
            Tip = "LanTransfer"
        };
    }

    private delegate IntPtr WindowProcedure(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Style;
        public WindowProcedure WindowProcedure;
        public int ClassExtra;
        public int WindowExtra;
        public IntPtr Instance;
        public IntPtr Icon;
        public IntPtr Cursor;
        public IntPtr Background;
        public string? MenuName;
        public string ClassName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint Size;
        public IntPtr Window;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public IntPtr Icon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Tip;

        public uint State;
        public uint StateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Info;

        public uint TimeoutOrVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string InfoTitle;

        public uint InfoFlags;
        public Guid GuidItem;
        public IntPtr BalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr Window;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public Point Point;
        public uint Private;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClass(ref WindowClass windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(uint extendedStyle, string className, string windowName, uint style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out NativeMessage message, IntPtr window, uint minMessage, uint maxMessage);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref NativeMessage message);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref NativeMessage message);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int exitCode);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr instance, IntPtr iconName);

    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShellNotifyIcon(uint message, ref NotifyIconData data);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenu(IntPtr menu, uint flags, uint identifier, string? text);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr window, IntPtr rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);
}
