namespace Dragonfly44.Application.Common.Models;

[Flags]
public enum KeyModifiers
{
    None = 0,
    Control = 1,
    Shift = 2,
    Alt = 4,
    Windows = 8
}

public record KeyboardShortcut(
    string Id,
    string Key,
    KeyModifiers Modifiers,
    Action Action,
    string Description = "",
    bool AllowDuringTextEditing = false,
    int Priority = 0);
