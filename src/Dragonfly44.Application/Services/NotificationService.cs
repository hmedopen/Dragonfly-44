using System.Collections.Concurrent;
using Dragonfly44.Application.Common.Enums;
using Dragonfly44.Application.Common.Models;
using Dragonfly44.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Dragonfly44.Application.Services;

public class NotificationService : INotificationService
{
    private readonly ConcurrentDictionary<Guid, NotificationItem> _notifications = new();
    private readonly ILogger<NotificationService> _logger;

    public event EventHandler<NotificationItem>? NotificationPublished;
    public event EventHandler<Guid>? NotificationDismissed;

    public IReadOnlyList<NotificationItem> ActiveNotifications =>
        _notifications.Values.OrderByDescending(n => n.CreatedAt).ToList().AsReadOnly();

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
    }

    public Guid Show(
        string message,
        string title = "Notification",
        NotificationSeverity severity = NotificationSeverity.Information,
        TimeSpan? duration = null,
        string? actionLabel = null,
        Action? actionCallback = null)
    {
        var id = Guid.NewGuid();
        var item = new NotificationItem(
            id,
            title,
            message,
            severity,
            DateTime.UtcNow,
            duration ?? TimeSpan.FromSeconds(5),
            actionLabel,
            actionCallback);

        _notifications[id] = item;
        _logger.LogInformation("Notification [{Severity}] '{Title}': {Message}", severity, title, message);

        NotificationPublished?.Invoke(this, item);

        if (item.Duration.HasValue && item.Duration.Value > TimeSpan.Zero)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(item.Duration.Value);
                Dismiss(id);
            });
        }

        return id;
    }

    public void ShowInfo(string message, string title = "Information") =>
        Show(message, title, NotificationSeverity.Information);

    public void ShowSuccess(string message, string title = "Success") =>
        Show(message, title, NotificationSeverity.Success);

    public void ShowWarning(string message, string title = "Warning") =>
        Show(message, title, NotificationSeverity.Warning);

    public void ShowError(string message, string title = "Error") =>
        Show(message, title, NotificationSeverity.Error);

    public void Dismiss(Guid notificationId)
    {
        if (_notifications.TryRemove(notificationId, out _))
        {
            NotificationDismissed?.Invoke(this, notificationId);
            _logger.LogDebug("Notification {Id} dismissed", notificationId);
        }
    }

    public void ClearAll()
    {
        var ids = _notifications.Keys.ToList();
        _notifications.Clear();
        foreach (var id in ids)
        {
            NotificationDismissed?.Invoke(this, id);
        }
    }
}
