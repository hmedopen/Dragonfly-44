using Dragonfly44.Application.Common.Enums;
using Dragonfly44.Application.Common.Models;
using Dragonfly44.Application.Interfaces;
using Dragonfly44.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dragonfly44.Tests;

public class WindowStateServiceTests
{
    private class InMemorySettingsService : ISettingsService
    {
        private readonly Dictionary<string, object> _data = new();

        public T Get<T>(string key, T defaultValue)
        {
            if (_data.TryGetValue(key, out var val) && val is T typed) return typed;
            return defaultValue;
        }

        public void Set<T>(string key, T value)
        {
            if (value != null) _data[key] = value;
        }

        public bool HasKey(string key) => _data.ContainsKey(key);
        public void Remove(string key) => _data.Remove(key);
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public void UpdateBounds_ShouldUpdateCurrentBoundsAndFireEvent()
    {
        var settings = new InMemorySettingsService();
        var service = new WindowStateService(settings, NullLogger<WindowStateService>.Instance);
        WindowBounds? notifiedBounds = null;
        service.BoundsChanged += (s, b) => notifiedBounds = b;

        service.UpdateBounds(200, 150, 1440, 900);

        service.CurrentBounds.X.Should().Be(200);
        service.CurrentBounds.Y.Should().Be(150);
        service.CurrentBounds.Width.Should().Be(1440);
        service.CurrentBounds.Height.Should().Be(900);
        notifiedBounds.Should().NotBeNull();
        notifiedBounds!.Width.Should().Be(1440);
    }

    [Fact]
    public void UpdateState_ShouldUpdateStateAndFireEvent()
    {
        var settings = new InMemorySettingsService();
        var service = new WindowStateService(settings, NullLogger<WindowStateService>.Instance);
        WindowState? notifiedState = null;
        service.StateChanged += (s, st) => notifiedState = st;

        service.UpdateState(WindowState.Maximized);

        service.CurrentState.Should().Be(WindowState.Maximized);
        notifiedState.Should().Be(WindowState.Maximized);
    }

    [Fact]
    public void UpdateDpi_ShouldUpdateDpiScaleAndFireEvent()
    {
        var settings = new InMemorySettingsService();
        var service = new WindowStateService(settings, NullLogger<WindowStateService>.Instance);
        double? notifiedDpi = null;
        service.DpiChanged += (s, dpi) => notifiedDpi = dpi;

        service.UpdateDpi(1.5);

        service.CurrentDpiScale.Should().Be(1.5);
        notifiedDpi.Should().Be(1.5);
    }

    [Fact]
    public async Task SaveAndLoadState_ShouldPersistAndRestoreBounds()
    {
        var settings = new InMemorySettingsService();
        var service = new WindowStateService(settings, NullLogger<WindowStateService>.Instance);

        service.UpdateBounds(350, 200, 1600, 1000);
        service.UpdateState(WindowState.Normal);
        service.UpdateDpi(1.25);

        await service.SaveStateAsync();

        var newService = new WindowStateService(settings, NullLogger<WindowStateService>.Instance);
        var restored = await newService.LoadStateAsync();

        restored.Should().NotBeNull();
        restored!.X.Should().Be(350);
        restored.Y.Should().Be(200);
        restored.Width.Should().Be(1600);
        restored.Height.Should().Be(1000);
        restored.DpiScale.Should().Be(1.25);
    }
}
