namespace Dragonfly44.Application.Common.Models;

public record DialogOptions(
    string Title,
    string Message,
    string PrimaryButtonText = "OK",
    string? SecondaryButtonText = null,
    string? CloseButtonText = "Cancel",
    bool IsDestructive = false);

public enum DialogResult
{
    None,
    Primary,
    Secondary,
    Cancel
}
