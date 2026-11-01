using Dragonfly44.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dragonfly44.Tests;

public class NavigationServiceTests
{
    private readonly NavigationService _service;

    public NavigationServiceTests()
    {
        _service = new NavigationService(NullLogger<NavigationService>.Instance);
    }

    [Fact]
    public void NavigateTo_ShouldUpdateCurrentViewAndRaiseNavigated()
    {
        string? navigatedKey = null;
        _service.Navigated += (s, k) => navigatedKey = k;

        var result = _service.NavigateTo("EditorView");

        result.Should().BeTrue();
        _service.CurrentViewKey.Should().Be("EditorView");
        _service.CanGoBack.Should().BeFalse();
        navigatedKey.Should().Be("EditorView");
    }

    [Fact]
    public void NavigateToMultiple_ShouldBuildBackStackAndAllowGoBack()
    {
        _service.NavigateTo("HomeView");
        _service.NavigateTo("EditorView");
        _service.NavigateTo("SettingsView");

        _service.CurrentViewKey.Should().Be("SettingsView");
        _service.CanGoBack.Should().BeTrue();

        var wentBack1 = _service.GoBack();
        wentBack1.Should().BeTrue();
        _service.CurrentViewKey.Should().Be("EditorView");

        var wentBack2 = _service.GoBack();
        wentBack2.Should().BeTrue();
        _service.CurrentViewKey.Should().Be("HomeView");
        _service.CanGoBack.Should().BeFalse();
    }

    [Fact]
    public void GoBack_EmptyStack_ShouldReturnFalse()
    {
        var result = _service.GoBack();
        result.Should().BeFalse();
    }

    [Fact]
    public void ClearHistory_ShouldEmptyBackStack()
    {
        _service.NavigateTo("View1");
        _service.NavigateTo("View2");

        _service.ClearHistory();

        _service.CanGoBack.Should().BeFalse();
    }
}
