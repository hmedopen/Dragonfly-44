using Dragonfly44.Application.Interfaces;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace Dragonfly44.Presentation.Input;

public class FocusService : IFocusService
{
    private readonly Stack<UIElement> _focusHistory = new();
    private XamlRoot? _xamlRoot;

    public void RegisterRoot(FrameworkElement root)
    {
        _xamlRoot = root?.XamlRoot;
        if (root != null)
        {
            root.Loaded += (s, e) => _xamlRoot = root.XamlRoot;
        }
    }

    public bool IsFocusInTextInput()
    {
        if (_xamlRoot == null) return false;
        var focused = FocusManager.GetFocusedElement(_xamlRoot);
        return InputRouter.IsTextEditorElement(focused);
    }

    public void PushFocus()
    {
        if (_xamlRoot == null) return;
        var focused = FocusManager.GetFocusedElement(_xamlRoot) as UIElement;
        if (focused != null)
        {
            _focusHistory.Push(focused);
        }
    }

    public void PopFocus()
    {
        if (_focusHistory.Count > 0)
        {
            var previous = _focusHistory.Pop();
            previous?.Focus(FocusState.Programmatic);
        }
    }
}
