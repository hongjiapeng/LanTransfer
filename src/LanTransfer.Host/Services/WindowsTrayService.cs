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
    private const int WmRButtonUp = 0x0205;
    private const int TrayCallbackMessage = WmApp + 1;
    private const uint ActivateMessage = WmApp + 2;
    private const string TrayWindowClassName = "LanTransferTrayWindow";
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
    private IntPtr _trayIcon;
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

    public static bool TryActivateExistingInstance()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var window = FindWindow(TrayWindowClassName, null);
            if (window != IntPtr.Zero && PostMessage(window, ActivateMessage, IntPtr.Zero, IntPtr.Zero))
            {
                return true;
            }

            Thread.Sleep(50);
        }

        return false;
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
            var className = TrayWindowClassName;
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

            _trayIcon = TryCreateTrayIcon();
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
            if (_trayIcon != IntPtr.Zero)
            {
                DestroyIcon(_trayIcon);
                _trayIcon = IntPtr.Zero;
            }
            _windowHandle = IntPtr.Zero;
        }
    }

    private IntPtr HandleWindowMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == ActivateMessage)
        {
            BrowserLauncher.TryOpen(_urls.LocalUrl, _logger);
            return IntPtr.Zero;
        }

        if (message == TrayCallbackMessage)
        {
            switch (unchecked((int)lParam.ToInt64()))
            {
                case WmLButtonUp:
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

    private NotifyIconData CreateIconData(IntPtr window)
    {
        return new NotifyIconData
        {
            Size = (uint)Marshal.SizeOf<NotifyIconData>(),
            Window = window,
            Id = 1,
            Flags = NifMessage | NifIcon | NifTip,
            CallbackMessage = TrayCallbackMessage,
            Icon = _trayIcon != IntPtr.Zero ? _trayIcon : LoadIcon(IntPtr.Zero, IdiApplication),
            Tip = "LanTransfer"
        };
    }

    private IntPtr TryCreateTrayIcon()
    {
        try
        {
            return CreateTrayIcon();
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Could not create the LanTransfer tray artwork; using the Windows default icon.");
            return IntPtr.Zero;
        }
    }

    private static IntPtr CreateTrayIcon()
    {
        const int size = 32;
        var pixels = new byte[size * size * 4];

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (IsInsideRoundedSquare(x, y, size, radius: 7))
                {
                    SetPixel(pixels, size, x, y, red: 29, green: 111, blue: 242, alpha: 255);
                }
            }
        }

        DrawRectangleOutline(pixels, size, left: 7, top: 7, right: 24, bottom: 20, thickness: 2);
        FillRectangle(pixels, size, left: 14, top: 21, right: 17, bottom: 24, red: 255, green: 255, blue: 255);
        FillRectangle(pixels, size, left: 10, top: 25, right: 21, bottom: 27, red: 255, green: 255, blue: 255);

        var bitmapInfo = new BitmapInfo
        {
            Header = new BitmapInfoHeader
            {
                Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                Width = size,
                Height = -size,
                Planes = 1,
                BitCount = 32,
                Compression = 0,
                SizeImage = (uint)pixels.Length
            }
        };

        var screen = GetDC(IntPtr.Zero);
        var colorBitmap = CreateDIBSection(screen, ref bitmapInfo, 0, out var pixelBuffer, IntPtr.Zero, 0);
        ReleaseDC(IntPtr.Zero, screen);
        if (colorBitmap == IntPtr.Zero || pixelBuffer == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the tray color bitmap.");
        }

        var maskBitmap = IntPtr.Zero;
        try
        {
            Marshal.Copy(pixels, 0, pixelBuffer, pixels.Length);
            maskBitmap = CreateBitmap(size, size, 1, 1, new byte[size * size / 8]);
            if (maskBitmap == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the tray mask bitmap.");
            }

            var iconInfo = new IconInfo
            {
                IsIcon = true,
                MaskBitmap = maskBitmap,
                ColorBitmap = colorBitmap
            };
            var icon = CreateIconIndirect(ref iconInfo);
            if (icon == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the tray icon.");
            }

            return icon;
        }
        finally
        {
            if (maskBitmap != IntPtr.Zero)
            {
                DeleteObject(maskBitmap);
            }

            DeleteObject(colorBitmap);
        }
    }

    private static bool IsInsideRoundedSquare(int x, int y, int size, int radius)
    {
        if ((x >= radius && x < size - radius) || (y >= radius && y < size - radius))
        {
            return true;
        }

        var centerX = x < radius ? radius : size - radius - 1;
        var centerY = y < radius ? radius : size - radius - 1;
        var deltaX = x - centerX;
        var deltaY = y - centerY;
        return deltaX * deltaX + deltaY * deltaY <= radius * radius;
    }

    private static void DrawRectangleOutline(
        byte[] pixels,
        int size,
        int left,
        int top,
        int right,
        int bottom,
        int thickness)
    {
        FillRectangle(pixels, size, left, top, right, top + thickness - 1, 255, 255, 255);
        FillRectangle(pixels, size, left, bottom - thickness + 1, right, bottom, 255, 255, 255);
        FillRectangle(pixels, size, left, top, left + thickness - 1, bottom, 255, 255, 255);
        FillRectangle(pixels, size, right - thickness + 1, top, right, bottom, 255, 255, 255);
    }

    private static void FillRectangle(
        byte[] pixels,
        int size,
        int left,
        int top,
        int right,
        int bottom,
        byte red,
        byte green,
        byte blue)
    {
        for (var y = top; y <= bottom; y++)
        {
            for (var x = left; x <= right; x++)
            {
                SetPixel(pixels, size, x, y, red, green, blue, alpha: 255);
            }
        }
    }

    private static void SetPixel(
        byte[] pixels,
        int size,
        int x,
        int y,
        byte red,
        byte green,
        byte blue,
        byte alpha)
    {
        var index = (y * size + x) * 4;
        pixels[index] = blue;
        pixels[index + 1] = green;
        pixels[index + 2] = red;
        pixels[index + 3] = alpha;
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

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ColorsUsed;
        public uint ColorsImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint Colors;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool IsIcon;

        public uint HotspotX;
        public uint HotspotY;
        public IntPtr MaskBitmap;
        public IntPtr ColorBitmap;
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

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindow(string? className, string? windowName);

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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateIconIndirect(ref IconInfo iconInfo);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr window, IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateDIBSection(
        IntPtr deviceContext,
        ref BitmapInfo bitmapInfo,
        uint usage,
        out IntPtr bits,
        IntPtr section,
        uint offset);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateBitmap(
        int width,
        int height,
        uint planes,
        uint bitsPerPixel,
        byte[] bits);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr graphicObject);

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
