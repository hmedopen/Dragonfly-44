using Dragonfly44.Application.Common.Enums;

namespace Dragonfly44.Application.Common.Models;

public record WindowBounds(
    int X,
    int Y,
    int Width,
    int Height,
    WindowState State = WindowState.Normal,
    double DpiScale = 1.0);
