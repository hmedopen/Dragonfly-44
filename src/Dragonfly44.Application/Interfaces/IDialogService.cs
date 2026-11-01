using Dragonfly44.Application.Common.Models;

namespace Dragonfly44.Application.Interfaces;

public interface IDialogService
{
    Task<DialogResult> ShowConfirmationAsync(
        string title,
        string message,
        string confirmText = "Confirm",
        string cancelText = "Cancel",
        bool isDestructive = false,
        CancellationToken cancellationToken = default);

    Task ShowMessageAsync(
        string title,
        string message,
        string closeText = "OK",
        CancellationToken cancellationToken = default);

    Task<DialogResult> ShowCustomDialogAsync(
        DialogOptions options,
        CancellationToken cancellationToken = default);

    void CancelAllPendingRequests();
}
