using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Dragonfly44.Presentation.Controls;

/// <summary>
/// Architectural base class for custom controls in Dragonfly 44.
/// Enforces separation between appearance (ControlTemplate / VisualStateManager),
/// behavior (interaction states, input routing), and business logic.
/// </summary>
public abstract class CustomControlBase : Control
{
    private bool _isPointerOver;
    private bool _isPressed;

    protected bool IsPointerOverState => _isPointerOver;
    protected bool IsPressedState => _isPressed;

    protected CustomControlBase()
    {
        IsEnabledChanged += OnIsEnabledChangedInternal;
        Loaded += OnLoadedInternal;
        Unloaded += OnUnloadedInternal;
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        UpdateVisualStates(false);
    }

    private void OnLoadedInternal(object sender, RoutedEventArgs e)
    {
        OnControlLoaded();
        UpdateVisualStates(false);
    }

    private void OnUnloadedInternal(object sender, RoutedEventArgs e)
    {
        OnControlUnloaded();
    }

    private void OnIsEnabledChangedInternal(object sender, DependencyPropertyChangedEventArgs e)
    {
        UpdateVisualStates(true);
    }

    protected override void OnPointerEntered(PointerRoutedEventArgs e)
    {
        base.OnPointerEntered(e);
        _isPointerOver = true;
        UpdateVisualStates(true);
    }

    protected override void OnPointerExited(PointerRoutedEventArgs e)
    {
        base.OnPointerExited(e);
        _isPointerOver = false;
        _isPressed = false;
        UpdateVisualStates(true);
    }

    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (CapturePointer(e.Pointer))
        {
            _isPressed = true;
            UpdateVisualStates(true);
        }
    }

    protected override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isPressed)
        {
            ReleasePointerCapture(e.Pointer);
            _isPressed = false;
            UpdateVisualStates(true);
        }
    }

    protected override void OnPointerCaptureLost(PointerRoutedEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _isPressed = false;
        UpdateVisualStates(true);
    }

    protected override void OnGotFocus(RoutedEventArgs e)
    {
        base.OnGotFocus(e);
        VisualStateManager.GoToState(this, "Focused", true);
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        VisualStateManager.GoToState(this, "Unfocused", true);
    }

    protected virtual void UpdateVisualStates(bool useTransitions)
    {
        if (!IsEnabled)
        {
            VisualStateManager.GoToState(this, "Disabled", useTransitions);
        }
        else if (_isPressed)
        {
            VisualStateManager.GoToState(this, "Pressed", useTransitions);
        }
        else if (_isPointerOver)
        {
            VisualStateManager.GoToState(this, "PointerOver", useTransitions);
        }
        else
        {
            VisualStateManager.GoToState(this, "Normal", useTransitions);
        }

        if (FocusState != FocusState.Unfocused)
        {
            VisualStateManager.GoToState(this, "Focused", useTransitions);
        }
        else
        {
            VisualStateManager.GoToState(this, "Unfocused", useTransitions);
        }
    }

    protected virtual void OnControlLoaded() { }
    protected virtual void OnControlUnloaded() { }
}
