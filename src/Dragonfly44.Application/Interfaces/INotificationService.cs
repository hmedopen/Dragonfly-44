using Dragonfly44.Application.Common.Enums;
using Dragonfly44.Application.Common.Models;

namespace Dragonfly44.Application.Interfaces;

public interface INotificationService
{
    event EventHandler<NotificationItem> NotificationPublished;
    event EventHandler<Guid> NotificationDismissed;

    IReadOnlyList<NotificationItem> ActiveNotifications { get; }

    Guid Show(string message, string title = "Notification", NotificationSeverity severity = NotificationSeverity.Information, TimeSpan? duration = null, string? actionLabel = null, Action? actionCallback = null);
    void ShowInfo(string message, string title = "Information");
    void ShowSuccess(string message, string title = "Success");
    void ShowWarning(string message, string title = "Warning");
    void ShowError(string message, string title = "Error");
    void Dismiss(Guid notificationId);
    void ClearAll();
}
