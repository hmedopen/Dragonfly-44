using Dragonfly44.Application.Common.Models;

namespace Dragonfly44.Application.Interfaces;

public interface IDialogService
{
    Task<DialogResult> ShowConfirmationAsync(string title, string message, string confirmText = "Confirm", string cancelText = "Cancel", bool isDestructive = false);
    Task ShowMessageAsync(string title, string message, string closeText = "OK");
    Task<DialogResult> ShowCustomDialogAsync(DialogOptions options);
}
