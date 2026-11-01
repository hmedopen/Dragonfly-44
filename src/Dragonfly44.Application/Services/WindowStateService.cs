using Dragonfly44.Application.Common.Enums;
using Dragonfly44.Application.Common.Models;
using Dragonfly44.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Dragonfly44.Application.Services;

public class WindowStateService : IWindowStateService
{
    private const string WindowBoundsKey = "Window.Placement";
    private readonly ISettingsService _settingsService;
    private readonly ILogger<WindowStateService> _logger;

    public WindowBounds CurrentBounds { get; private set; } = new(100, 100, 1280, 800, WindowState.Normal, 1.0);
    public WindowState CurrentState => CurrentBounds.State;
    public double CurrentDpiScale => CurrentBounds.DpiScale;

    public event EventHandler<WindowBounds>? BoundsChanged;
    public event EventHandler<WindowState>? StateChanged;
    public event EventHandler<double>? DpiChanged;

    public WindowStateService(
        ISettingsService settingsService,
        ILogger<WindowStateService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    public void UpdateBounds(int x, int y, int width, int height)
    {
        CurrentBounds = CurrentBounds with { X = x, Y = y, Width = width, Height = height };
        BoundsChanged?.Invoke(this, CurrentBounds);
    }

    public void UpdateState(WindowState state)
    {
        if (CurrentBounds.State != state)
        {
            CurrentBounds = CurrentBounds with { State = state };
            StateChanged?.Invoke(this, state);
        }
    }

    public void UpdateDpi(double dpiScale)
    {
        if (Math.Abs(CurrentBounds.DpiScale - dpiScale) > 0.001)
        {
            CurrentBounds = CurrentBounds with { DpiScale = dpiScale };
            DpiChanged?.Invoke(this, dpiScale);
        }
    }

    public async Task SaveStateAsync(CancellationToken cancellationToken = default)
    {
        _settingsService.Set(WindowBoundsKey, CurrentBounds);
        await _settingsService.SaveAsync(cancellationToken);
        _logger.LogDebug("Saved window state: {@Bounds}", CurrentBounds);
    }

    public Task<WindowBounds?> LoadStateAsync(CancellationToken cancellationToken = default)
    {
        if (_settingsService.HasKey(WindowBoundsKey))
        {
            var saved = _settingsService.Get<WindowBounds?>(WindowBoundsKey, null);
            if (saved != null)
            {
                CurrentBounds = saved;
                _logger.LogInformation("Restored window bounds from settings: {@Bounds}", saved);
                return Task.FromResult<WindowBounds?>(saved);
            }
        }

        return Task.FromResult<WindowBounds?>(null);
    }
}
