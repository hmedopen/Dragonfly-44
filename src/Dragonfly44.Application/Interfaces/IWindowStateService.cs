using Dragonfly44.Application.Common.Enums;
using Dragonfly44.Application.Common.Models;

namespace Dragonfly44.Application.Interfaces;

public interface IWindowStateService
{
    WindowBounds CurrentBounds { get; }
    WindowState CurrentState { get; }
    double CurrentDpiScale { get; }

    event EventHandler<WindowBounds> BoundsChanged;
    event EventHandler<WindowState> StateChanged;
    event EventHandler<double> DpiChanged;

    void UpdateBounds(int x, int y, int width, int height);
    void UpdateState(WindowState state);
    void UpdateDpi(double dpiScale);

    Task SaveStateAsync(CancellationToken cancellationToken = default);
    Task<WindowBounds?> LoadStateAsync(CancellationToken cancellationToken = default);
}
