using System.Runtime.InteropServices;
using Dragonfly44.Application.Common.Enums;
using Dragonfly44.Application.Common.Models;
using Dragonfly44.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace Dragonfly44.Presentation.Windowing;

public class WindowController : IWindowController
{
    private readonly IWindowStateService _windowStateService;
    private readonly ILogger<WindowController> _logger;

    private Window? _window;
    private AppWindow? _appWindow;
    private OverlappedPresenter? _presenter;
    private IntPtr _hwnd = IntPtr.Zero;
    private FrameworkElement? _dragElement;

    public WindowState CurrentState { get; private set; } = WindowState.Normal;
    public WindowBounds CurrentBounds { get; private set; } = new(100, 100, 1280, 800);
    public double CurrentDpiScale { get; private set; } = 1.0;
    public bool IsMaximized => CurrentState == WindowState.Maximized;

    public event EventHandler<WindowStateChangedEventArgs>? StateChanged;
    public event EventHandler<double>? DpiChanged;

    public WindowController(
        IWindowStateService windowStateService,
        ILogger<WindowController> logger)
    {
        _windowStateService = windowStateService;
        _logger = logger;
    }

    public void Attach(Window window, FrameworkElement? dragRegionElement = null)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _hwnd = WindowNative.GetWindowHandle(_window);

        var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            _presenter = presenter;
            _presenter.IsResizable = true;
            _presenter.IsMinimizable = true;
            _presenter.IsMaximizable = true;
        }

        // Enable custom titlebar extending content into titlebar area
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            var titleBar = _appWindow.TitleBar;
            titleBar.ExtendsContentIntoTitleBar = true;

            // Make native caption buttons transparent to give full custom control
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = Colors.Transparent;
            titleBar.ButtonInactiveForegroundColor = Colors.Transparent;
            titleBar.ButtonHoverBackgroundColor = Colors.Transparent;
            titleBar.ButtonPressedBackgroundColor = Colors.Transparent;
        }
        else
        {
            _window.ExtendsContentIntoTitleBar = true;
        }

        // Update DPI
        UpdateDpiScale();

        // Listen to window size and state changes
        _appWindow.Changed += OnAppWindowChanged;

        if (dragRegionElement != null)
        {
            SetDragRegion(dragRegionElement);
        }

        // Hook XamlRoot for DPI changes
        if (_window.Content is FrameworkElement root)
        {
            root.Loaded += (s, e) =>
            {
                if (root.XamlRoot != null)
                {
                    root.XamlRoot.Changed += (sender, args) => UpdateDpiScale();
                }
                UpdateDragRectangles();
            };
            root.SizeChanged += (s, e) => UpdateDragRectangles();
        }

        _logger.LogInformation("WindowController attached to window with HWND {Hwnd}", _hwnd);
    }

    public void SetDragRegion(FrameworkElement dragRegionElement)
    {
        _dragElement = dragRegionElement;
        if (_window != null)
        {
            _window.SetTitleBar(_dragElement);
        }

        if (_dragElement != null)
        {
            _dragElement.SizeChanged += (s, e) => UpdateDragRectangles();
        }

        UpdateDragRectangles();
    }

    private void UpdateDragRectangles()
    {
        if (_appWindow == null || !AppWindowTitleBar.IsCustomizationSupported() || _dragElement == null)
        {
            return;
        }

        try
        {
            var transform = _dragElement.TransformToVisual(null);
            var bounds = transform.TransformBounds(new Windows.Foundation.Rect(0, 0, _dragElement.ActualWidth, _dragElement.ActualHeight));
            var scale = CurrentDpiScale > 0 ? CurrentDpiScale : 1.0;

            var dragRect = new RectInt32(
                (int)(bounds.X * scale),
                (int)(bounds.Y * scale),
                (int)(bounds.Width * scale),
                (int)(bounds.Height * scale));

            _appWindow.TitleBar.SetDragRectangles(new[] { dragRect });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to update drag rectangles");
        }
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidPositionChange || args.DidSizeChange || args.DidPresenterChange)
        {
            var newState = _presenter?.State switch
            {
                OverlappedPresenterState.Maximized => WindowState.Maximized,
                OverlappedPresenterState.Minimized => WindowState.Minimized,
                _ => WindowState.Normal
            };

            var position = sender.Position;
            var size = sender.Size;

            CurrentBounds = new WindowBounds(position.X, position.Y, size.Width, size.Height, newState, CurrentDpiScale);
            CurrentState = newState;

            _windowStateService.UpdateBounds(position.X, position.Y, size.Width, size.Height);
            _windowStateService.UpdateState(newState);

            StateChanged?.Invoke(this, new WindowStateChangedEventArgs(newState, CurrentBounds));
        }
    }

    private void UpdateDpiScale()
    {
        var dpi = GetDpiForWindow(_hwnd);
        var scale = dpi > 0 ? dpi / 96.0 : 1.0;

        if (Math.Abs(scale - CurrentDpiScale) > 0.001)
        {
            CurrentDpiScale = scale;
            _windowStateService.UpdateDpi(CurrentDpiScale);
            DpiChanged?.Invoke(this, CurrentDpiScale);
            UpdateDragRectangles();
            _logger.LogInformation("Window DPI scale updated to {Scale} (DPI {Dpi})", CurrentDpiScale, dpi);
        }
    }

    public void Minimize()
    {
        _presenter?.Minimize();
    }

    public void Maximize()
    {
        _presenter?.Maximize();
    }

    public void Restore()
    {
        _presenter?.Restore();
    }

    public void ToggleMaximize()
    {
        if (CurrentState == WindowState.Maximized)
        {
            Restore();
        }
        else
        {
            Maximize();
        }
    }

    public void Close()
    {
        _window?.Close();
    }

    public void SetBounds(int x, int y, int width, int height)
    {
        if (_appWindow == null) return;

        // Ensure window coordinates sit on a valid display
        var validated = ValidateCoordinates(x, y, width, height);
        _appWindow.MoveAndResize(new RectInt32(validated.X, validated.Y, validated.Width, validated.Height));
    }

    private (int X, int Y, int Width, int Height) ValidateCoordinates(int x, int y, int width, int height)
    {
        const int minWidth = 640;
        const int minHeight = 480;

        var w = Math.Max(minWidth, width);
        var h = Math.Max(minHeight, height);

        var pt = new POINT { x = x, y = y };
        var hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);

        if (hMonitor != IntPtr.Zero)
        {
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(hMonitor, ref mi))
            {
                var workArea = mi.rcWork;
                var clampedX = Math.Clamp(x, workArea.left, Math.Max(workArea.left, workArea.right - 100));
                var clampedY = Math.Clamp(y, workArea.top, Math.Max(workArea.top, workArea.bottom - 100));
                return (clampedX, clampedY, Math.Min(w, workArea.right - workArea.left), Math.Min(h, workArea.bottom - workArea.top));
            }
        }

        return (x, y, w, h);
    }

    #region Win32 P/Invoke
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
    #endregion
}
