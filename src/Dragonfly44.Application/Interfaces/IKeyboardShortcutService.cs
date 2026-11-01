using Dragonfly44.Application.Common.Models;

namespace Dragonfly44.Application.Interfaces;

public interface IKeyboardShortcutService
{
    IReadOnlyCollection<KeyboardShortcut> RegisteredShortcuts { get; }

    void Register(KeyboardShortcut shortcut);
    bool Unregister(string shortcutId);
    void Clear();

    bool HandleKeyDown(string key, KeyModifiers modifiers, bool isTextEditingContext);
}
