using Dragonfly44.Application.Common.Models;
using Dragonfly44.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace Dragonfly44.Presentation.Input;

public class InputRouter
{
    private readonly IKeyboardShortcutService _shortcutService;
    private readonly ILogger<InputRouter> _logger;
    private FrameworkElement? _rootElement;

    public InputRouter(
        IKeyboardShortcutService shortcutService,
        ILogger<InputRouter> logger)
    {
        _shortcutService = shortcutService;
        _logger = logger;
    }

    public void Attach(FrameworkElement rootElement)
    {
        _rootElement = rootElement ?? throw new ArgumentNullException(nameof(rootElement));
        _rootElement.PreviewKeyDown += OnPreviewKeyDown;
        _logger.LogInformation("InputRouter attached to root element");
    }

    public void Detach()
    {
        if (_rootElement != null)
        {
            _rootElement.PreviewKeyDown -= OnPreviewKeyDown;
            _rootElement = null;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var key = e.Key.ToString();
        var modifiers = GetCurrentModifiers();
        var isTextEditing = IsFocusInTextEditingContext();

        if (_shortcutService.HandleKeyDown(key, modifiers, isTextEditing))
        {
            e.Handled = true;
        }
    }

    public bool IsFocusInTextEditingContext()
    {
        if (_rootElement?.XamlRoot == null) return false;

        var focusedElement = FocusManager.GetFocusedElement(_rootElement.XamlRoot);
        return IsTextEditorElement(focusedElement);
    }

    public static bool IsTextEditorElement(object? element)
    {
        if (element == null) return false;

        return element is TextBox
            or RichEditBox
            or PasswordBox
            or AutoSuggestBox;
    }

    public static KeyModifiers GetCurrentModifiers()
    {
        var modifiers = KeyModifiers.None;

        var ctrlState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control);
        if ((ctrlState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down)
        {
            modifiers |= KeyModifiers.Control;
        }

        var shiftState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
        if ((shiftState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down)
        {
            modifiers |= KeyModifiers.Shift;
        }

        var altState = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu);
        if ((altState & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down)
        {
            modifiers |= KeyModifiers.Alt;
        }

        return modifiers;
    }
}
