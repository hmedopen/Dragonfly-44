using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Dragonfly44.Presentation.Controls;

public class CustomButton : CustomControlBase
{
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
            new PropertyMetadata(null));

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
        if (e.Key is VirtualKey.Enter or VirtualKey.Space && IsEnabled)
        {
            PerformClick();
            e.Handled = true;
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
                button.IsEnabled = newCommand.CanExecute(button.CommandParameter);
            }
        }
    }

    private void OnCanExecuteChanged(object? sender, EventArgs e)
    {
        if (Command != null)
        {
            IsEnabled = Command.CanExecute(CommandParameter);
        }
    }
}
