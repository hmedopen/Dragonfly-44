using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Dragonfly44.Presentation.Controls;

public class CustomButton : CustomControlBase
{
    private bool _isSpaceKeyDown;
    private bool _isEnterKeyDown;

    public static readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(
            nameof(Content),
            typeof(object),
            typeof(CustomButton),
            new PropertyMetadata(null));

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(
            nameof(Command),
            typeof(ICommand),
            typeof(CustomButton),
            new PropertyMetadata(null, OnCommandChanged));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(
            nameof(CommandParameter),
            typeof(object),
            typeof(CustomButton),
            new PropertyMetadata(null, OnCommandParameterChanged));

    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public event RoutedEventHandler? Click;

    public CustomButton()
    {
        DefaultStyleKey = typeof(CustomButton);
        IsTabStop = true;
        IsEnabledChanged += (s, e) =>
        {
            if (!IsEnabled)
            {
                CancelPendingKeyboardActivation();
            }
        };
    }

    protected override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        var wasPressed = IsPressedState;
        base.OnPointerReleased(e);

        if (wasPressed && IsPointerOverState && IsEnabled)
        {
            PerformClick();
        }
    }

    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == VirtualKey.Space && IsEnabled)
        {
            if (!_isSpaceKeyDown)
            {
                _isSpaceKeyDown = true;
                SetPressedState(true);
            }
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Enter && IsEnabled)
        {
            // Enter activates once per deliberate activation; holding Enter does not repeat clicks
            if (!_isEnterKeyDown)
            {
                _isEnterKeyDown = true;
                PerformClick();
            }
            e.Handled = true;
        }
    }

    protected override void OnKeyUp(KeyRoutedEventArgs e)
    {
        base.OnKeyUp(e);

        if (e.Key == VirtualKey.Space)
        {
            if (_isSpaceKeyDown)
            {
                _isSpaceKeyDown = false;
                SetPressedState(false);
                if (IsEnabled)
                {
                    PerformClick();
                }
                e.Handled = true;
            }
        }
        else if (e.Key == VirtualKey.Enter)
        {
            if (_isEnterKeyDown)
            {
                _isEnterKeyDown = false;
                e.Handled = true;
            }
        }
    }

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        CancelPendingKeyboardActivation();
        base.OnLostFocus(e);
    }

    private void CancelPendingKeyboardActivation()
    {
        if (_isSpaceKeyDown || _isEnterKeyDown)
        {
            _isSpaceKeyDown = false;
            _isEnterKeyDown = false;
            SetPressedState(false);
        }
    }

    public void PerformClick()
    {
        Click?.Invoke(this, new RoutedEventArgs());

        if (Command != null && Command.CanExecute(CommandParameter))
        {
            Command.Execute(CommandParameter);
        }
    }

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CustomButton button)
        {
            if (e.OldValue is ICommand oldCommand)
            {
                oldCommand.CanExecuteChanged -= button.OnCanExecuteChanged;
            }
            if (e.NewValue is ICommand newCommand)
            {
                newCommand.CanExecuteChanged += button.OnCanExecuteChanged;
            }
            button.UpdateCanExecute();
        }
    }

    private static void OnCommandParameterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CustomButton button)
        {
            button.UpdateCanExecute();
        }
    }

    private void OnCanExecuteChanged(object? sender, EventArgs e)
    {
        UpdateCanExecute();
    }

    public void UpdateCanExecute()
    {
        if (Command != null)
        {
            IsEnabled = Command.CanExecute(CommandParameter);
        }
    }
}
