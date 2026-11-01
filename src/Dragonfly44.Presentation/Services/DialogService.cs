using Dragonfly44.Application.Common.Models;
using Dragonfly44.Application.Interfaces;
using Dragonfly44.Presentation.Controls;
using Microsoft.Extensions.Logging;

namespace Dragonfly44.Presentation.Services;

public class DialogService : IDialogService
{
    private readonly ILogger<DialogService> _logger;
    private CustomDialogHost? _dialogHost;

    public DialogService(ILogger<DialogService> logger)
    {
        _logger = logger;
    }

    public void RegisterHost(CustomDialogHost host)
    {
        _dialogHost = host ?? throw new ArgumentNullException(nameof(host));
        _logger.LogInformation("CustomDialogHost registered with DialogService");
    }

    public async Task<DialogResult> ShowConfirmationAsync(
        string title,
        string message,
        string confirmText = "Confirm",
        string cancelText = "Cancel",
        bool isDestructive = false)
    {
        var options = new DialogOptions(
            Title: title,
            Message: message,
            PrimaryButtonText: confirmText,
            CloseButtonText: cancelText,
            IsDestructive: isDestructive);

        return await ShowCustomDialogAsync(options);
    }

    public async Task ShowMessageAsync(string title, string message, string closeText = "OK")
    {
        var options = new DialogOptions(
            Title: title,
            Message: message,
            PrimaryButtonText: closeText,
            CloseButtonText: null);

        await ShowCustomDialogAsync(options);
    }

    public async Task<DialogResult> ShowCustomDialogAsync(DialogOptions options)
    {
        if (_dialogHost == null)
        {
            _logger.LogWarning("Dialog presentation requested without a registered CustomDialogHost. Returning Cancel.");
            return DialogResult.Cancel;
        }

        _logger.LogInformation("Presenting dialog: '{Title}'", options.Title);
        return await _dialogHost.ShowAsync(options);
    }
}
