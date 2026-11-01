using Dragonfly44.Application.Common.Enums;
using Dragonfly44.Application.Common.Models;
using Dragonfly44.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dragonfly44.Tests;

public class NotificationServiceTests
{
    private readonly NotificationService _service;

    public NotificationServiceTests()
    {
        _service = new NotificationService(NullLogger<NotificationService>.Instance);
    }

    [Fact]
    public void Show_ShouldPublishNotificationAndAddToList()
    {
        NotificationItem? publishedItem = null;
        _service.NotificationPublished += (s, item) => publishedItem = item;

        var id = _service.Show("Test message", "Test Title", NotificationSeverity.Success);

        id.Should().NotBeEmpty();
        _service.ActiveNotifications.Should().ContainSingle(n => n.Id == id);
        publishedItem.Should().NotBeNull();
        publishedItem!.Title.Should().Be("Test Title");
        publishedItem.Message.Should().Be("Test message");
        publishedItem.Severity.Should().Be(NotificationSeverity.Success);
    }

    [Fact]
    public void Dismiss_ShouldRemoveNotificationAndRaiseDismissedEvent()
    {
        var id = _service.Show("To be dismissed");
        Guid? dismissedId = null;
        _service.NotificationDismissed += (s, gid) => dismissedId = gid;

        _service.Dismiss(id);

        _service.ActiveNotifications.Should().BeEmpty();
        dismissedId.Should().Be(id);
    }

    [Fact]
    public void ClearAll_ShouldRemoveAllActiveNotifications()
    {
        _service.Show("Notification 1");
        _service.Show("Notification 2");
        _service.Show("Notification 3");

        _service.ActiveNotifications.Should().HaveCount(3);

        _service.ClearAll();

        _service.ActiveNotifications.Should().BeEmpty();
    }

    [Theory]
    [InlineData(NotificationSeverity.Information)]
    [InlineData(NotificationSeverity.Success)]
    [InlineData(NotificationSeverity.Warning)]
    [InlineData(NotificationSeverity.Error)]
    public void ShowSeverityMethods_ShouldSetCorrectSeverity(NotificationSeverity severity)
    {
        NotificationItem? received = null;
        _service.NotificationPublished += (s, n) => received = n;

        switch (severity)
        {
            case NotificationSeverity.Information:
                _service.ShowInfo("info");
                break;
            case NotificationSeverity.Success:
                _service.ShowSuccess("success");
                break;
            case NotificationSeverity.Warning:
                _service.ShowWarning("warning");
                break;
            case NotificationSeverity.Error:
                _service.ShowError("error");
                break;
        }

        received.Should().NotBeNull();
        received!.Severity.Should().Be(severity);
    }
}
