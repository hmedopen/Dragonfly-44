using Dragonfly44.Application.Common.Models;
using Dragonfly44.Application.Interfaces;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Dragonfly44.Presentation.Controls;

public class CustomDialogHost : ContentControl, IDialogPresenter
{
    private TaskCompletionSource<DialogResult>? _currentDialogTcs;
    private CancellationTokenRegistration? _currentCancellationReg;

    private CustomButton? _primaryButton;
    private CustomButton? _secondaryButton;
    private CustomButton? _cancelButton;

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
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DismissActiveDialog();
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_primaryButton != null) _primaryButton.Click -= OnPrimaryClicked;
        if (_secondaryButton != null) _secondaryButton.Click -= OnSecondaryClicked;
        if (_cancelButton != null) _cancelButton.Click -= OnCancelClicked;

        _primaryButton = GetTemplateChild("PART_PrimaryButton") as CustomButton;
        _secondaryButton = GetTemplateChild("PART_SecondaryButton") as CustomButton;
        _cancelButton = GetTemplateChild("PART_CancelButton") as CustomButton;

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

    public Task<DialogResult> ShowDialogAsync(DialogOptions options, CancellationToken cancellationToken = default)
    {
        if (DispatcherQueue != null && !DispatcherQueue.HasThreadAccess)
        {
            var tcs = new TaskCompletionSource<DialogResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            DispatcherQueue.TryEnqueue(() =>
            {
                _ = ShowDialogCoreAsync(options, cancellationToken).ContinueWith(t =>
                {
                    if (t.IsFaulted) tcs.TrySetException(t.Exception!);
                    else if (t.IsCanceled) tcs.TrySetCanceled();
                    else tcs.TrySetResult(t.Result);
                });
            });
            return tcs.Task;
        }

        return ShowDialogCoreAsync(options, cancellationToken);
    }

    private Task<DialogResult> ShowDialogCoreAsync(DialogOptions options, CancellationToken cancellationToken)
    {
        // Cancel any lingering earlier dialog
        _currentCancellationReg?.Dispose();
        _currentDialogTcs?.TrySetResult(DialogResult.Cancel);

        var tcs = new TaskCompletionSource<DialogResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _currentDialogTcs = tcs;

        if (cancellationToken.CanBeCanceled)
        {
            _currentCancellationReg = cancellationToken.Register(() =>
            {
                DismissActiveDialog();
            });
        }

        DialogTitle = options.Title;
        DialogMessage = options.Message;
        PrimaryButtonText = options.PrimaryButtonText;
        SecondaryButtonText = options.SecondaryButtonText ?? string.Empty;
        CancelButtonText = options.CloseButtonText ?? "Cancel";

        IsOpen = true;
        return tcs.Task;
    }

    public void DismissActiveDialog()
    {
        void Dismiss()
        {
            _currentCancellationReg?.Dispose();
            _currentCancellationReg = null;

            var tcs = _currentDialogTcs;
            _currentDialogTcs = null;
            IsOpen = false;
            tcs?.TrySetResult(DialogResult.Cancel);
        }

        if (DispatcherQueue != null && !DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(Dismiss);
        }
        else
        {
            Dismiss();
        }
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
        _currentCancellationReg?.Dispose();
        _currentCancellationReg = null;

        var tcs = _currentDialogTcs;
        _currentDialogTcs = null;
        IsOpen = false;
        tcs?.TrySetResult(result);
    }
}
