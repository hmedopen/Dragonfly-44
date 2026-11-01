using Dragonfly44.Application.Common.Enums;

namespace Dragonfly44.Application.Common.Models;

public record NotificationItem(
    Guid Id,
    string Title,
    string Message,
    NotificationSeverity Severity,
    DateTime CreatedAt,
    TimeSpan? Duration = null,
    string? ActionLabel = null,
    Action? ActionCallback = null);
