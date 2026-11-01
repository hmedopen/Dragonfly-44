using Dragonfly44.Application.Common.Models;
using Dragonfly44.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Dragonfly44.Application.Services;

public class DialogService : IDialogService
{
    private readonly SemaphoreSlim _dialogLock = new(1, 1);
    private readonly ILogger<DialogService> _logger;
    private IDialogPresenter? _presenter;

    public DialogService(ILogger<DialogService> logger)
    {
        _logger = logger;
    }

    public void RegisterPresenter(IDialogPresenter presenter)
    {
        _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        _logger.LogInformation("IDialogPresenter registered with DialogService.");
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
        if (_presenter == null)
        {
            _logger.LogWarning("Dialog presentation requested without a registered IDialogPresenter. Returning Cancel.");
            return DialogResult.Cancel;
        }

        // Single-dialog policy: await the lock so subsequent requests are queued in FIFO order
        await _dialogLock.WaitAsync();
        try
        {
            _logger.LogInformation("Presenting dialog: '{Title}'", options.Title);
            return await _presenter.ShowDialogAsync(options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error presenting dialog: '{Title}'", options.Title);
            return DialogResult.Cancel;
        }
        finally
        {
            _dialogLock.Release();
        }
    }
}
