using Dragonfly44.Application.Common.Enums;
using Dragonfly44.Application.Common.Models;
using Microsoft.UI.Xaml;

namespace Dragonfly44.Presentation.Windowing;

public class WindowStateChangedEventArgs : EventArgs
{
    public WindowState NewState { get; }
    public WindowBounds Bounds { get; }

    public WindowStateChangedEventArgs(WindowState newState, WindowBounds bounds)
    {
        NewState = newState;
        Bounds = bounds;
    }
}

public interface IWindowController
{
    WindowState CurrentState { get; }
    WindowBounds CurrentBounds { get; }
    double CurrentDpiScale { get; }
    bool IsMaximized { get; }

    event EventHandler<WindowStateChangedEventArgs>? StateChanged;
    event EventHandler<double>? DpiChanged;

    void Attach(Window window, FrameworkElement? dragRegionElement = null);
    void SetDragRegion(FrameworkElement dragRegionElement);
    void Minimize();
    void Maximize();
    void Restore();
    void ToggleMaximize();
    void Close();
    void SetBounds(int x, int y, int width, int height);
}
