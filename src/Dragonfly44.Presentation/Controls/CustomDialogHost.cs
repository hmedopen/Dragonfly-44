using Dragonfly44.Application.Common.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Dragonfly44.Presentation.Controls;

public class CustomDialogHost : ContentControl
{
    private TaskCompletionSource<DialogResult>? _dialogTcs;
    private Button? _primaryButton;
    private Button? _secondaryButton;
    private Button? _cancelButton;

    public static readonly DependencyProperty IsOpenProperty =
        DependencyProperty.Register(
            nameof(IsOpen),
            typeof(bool),
            typeof(CustomDialogHost),
            new PropertyMetadata(false, OnIsOpenChanged));

    public static readonly DependencyProperty DialogTitleProperty =
        DependencyProperty.Register(
            nameof(DialogTitle),
            typeof(string),
            typeof(CustomDialogHost),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DialogMessageProperty =
        DependencyProperty.Register(
            nameof(DialogMessage),
            typeof(string),
            typeof(CustomDialogHost),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty PrimaryButtonTextProperty =
        DependencyProperty.Register(
            nameof(PrimaryButtonText),
            typeof(string),
            typeof(CustomDialogHost),
            new PropertyMetadata("OK"));

    public static readonly DependencyProperty SecondaryButtonTextProperty =
        DependencyProperty.Register(
            nameof(SecondaryButtonText),
            typeof(string),
            typeof(CustomDialogHost),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty CancelButtonTextProperty =
        DependencyProperty.Register(
            nameof(CancelButtonText),
            typeof(string),
            typeof(CustomDialogHost),
            new PropertyMetadata("Cancel"));

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public string DialogTitle
    {
        get => (string)GetValue(DialogTitleProperty);
        set => SetValue(DialogTitleProperty, value);
    }

    public string DialogMessage
    {
        get => (string)GetValue(DialogMessageProperty);
        set => SetValue(DialogMessageProperty, value);
    }

    public string PrimaryButtonText
    {
        get => (string)GetValue(PrimaryButtonTextProperty);
        set => SetValue(PrimaryButtonTextProperty, value);
    }

    public string SecondaryButtonText
    {
        get => (string)GetValue(SecondaryButtonTextProperty);
        set => SetValue(SecondaryButtonTextProperty, value);
    }

    public string CancelButtonText
    {
        get => (string)GetValue(CancelButtonTextProperty);
        set => SetValue(CancelButtonTextProperty, value);
    }

    public CustomDialogHost()
    {
        DefaultStyleKey = typeof(CustomDialogHost);
        Visibility = Visibility.Collapsed;
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_primaryButton != null) _primaryButton.Click -= OnPrimaryClicked;
        if (_secondaryButton != null) _secondaryButton.Click -= OnSecondaryClicked;
        if (_cancelButton != null) _cancelButton.Click -= OnCancelClicked;

        _primaryButton = GetTemplateChild("PART_PrimaryButton") as Button;
        _secondaryButton = GetTemplateChild("PART_SecondaryButton") as Button;
        _cancelButton = GetTemplateChild("PART_CancelButton") as Button;

        if (_primaryButton != null) _primaryButton.Click += OnPrimaryClicked;
        if (_secondaryButton != null) _secondaryButton.Click += OnSecondaryClicked;
        if (_cancelButton != null) _cancelButton.Click += OnCancelClicked;

        UpdateVisibility();
    }

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CustomDialogHost host)
        {
            host.UpdateVisibility();
        }
    }

    private void UpdateVisibility()
    {
        Visibility = IsOpen ? Visibility.Visible : Visibility.Collapsed;
    }

    public Task<DialogResult> ShowAsync(DialogOptions options)
    {
        _dialogTcs = new TaskCompletionSource<DialogResult>();

        DialogTitle = options.Title;
        DialogMessage = options.Message;
        PrimaryButtonText = options.PrimaryButtonText;
        SecondaryButtonText = options.SecondaryButtonText ?? string.Empty;
        CancelButtonText = options.CloseButtonText ?? "Cancel";

        IsOpen = true;

        return _dialogTcs.Task;
    }

    private void OnPrimaryClicked(object sender, RoutedEventArgs e)
    {
        CloseWithResult(DialogResult.Primary);
    }

    private void OnSecondaryClicked(object sender, RoutedEventArgs e)
    {
        CloseWithResult(DialogResult.Secondary);
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        CloseWithResult(DialogResult.Cancel);
    }

    private void CloseWithResult(DialogResult result)
    {
        IsOpen = false;
        _dialogTcs?.TrySetResult(result);
    }
}
