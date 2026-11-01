using System.Collections.Concurrent;
using Dragonfly44.Application.Common.Models;
using Dragonfly44.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Dragonfly44.Application.Services;

public class KeyboardShortcutService : IKeyboardShortcutService
{
    private readonly ConcurrentDictionary<string, KeyboardShortcut> _shortcuts = new();
    private readonly ILogger<KeyboardShortcutService> _logger;

    public IReadOnlyCollection<KeyboardShortcut> RegisteredShortcuts => _shortcuts.Values.ToList().AsReadOnly();

    public KeyboardShortcutService(ILogger<KeyboardShortcutService> logger)
    {
        _logger = logger;
    }

    public void Register(KeyboardShortcut shortcut)
    {
        if (shortcut == null) throw new ArgumentNullException(nameof(shortcut));
        _shortcuts[shortcut.Id] = shortcut;
        _logger.LogDebug("Registered keyboard shortcut '{Id}': {Modifiers}+{Key} (AllowDuringTextEditing: {Allow})",
            shortcut.Id, shortcut.Modifiers, shortcut.Key, shortcut.AllowDuringTextEditing);
    }

    public bool Unregister(string shortcutId)
    {
        var removed = _shortcuts.TryRemove(shortcutId, out _);
        if (removed)
        {
            _logger.LogDebug("Unregistered keyboard shortcut '{Id}'", shortcutId);
        }
        return removed;
    }

    public void Clear()
    {
        _shortcuts.Clear();
    }

    public bool HandleKeyDown(string key, KeyModifiers modifiers, bool isTextEditingContext)
    {
        var matchingShortcuts = _shortcuts.Values
            .Where(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase) && s.Modifiers == modifiers)
            .OrderByDescending(s => s.Priority)
            .ToList();

        if (matchingShortcuts.Count == 0)
        {
            return false;
        }

        foreach (var shortcut in matchingShortcuts)
        {
            if (isTextEditingContext && !shortcut.AllowDuringTextEditing)
            {
                _logger.LogDebug("Shortcut '{Id}' suppressed because focus is in a text editing control", shortcut.Id);
                continue;
            }

            try
            {
                _logger.LogInformation("Executing shortcut '{Id}' ({Modifiers}+{Key})", shortcut.Id, shortcut.Modifiers, shortcut.Key);
                shortcut.Action.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing shortcut '{Id}'", shortcut.Id);
            }
        }

        return false;
    }
}
