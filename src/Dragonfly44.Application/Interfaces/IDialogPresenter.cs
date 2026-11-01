using Dragonfly44.Application.Common.Models;

namespace Dragonfly44.Application.Interfaces;

public interface IDialogPresenter
{
    Task<DialogResult> ShowDialogAsync(DialogOptions options, CancellationToken cancellationToken = default);
    void DismissActiveDialog();
}
