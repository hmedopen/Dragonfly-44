using Dragonfly44.Application.Interfaces;
using Dragonfly44.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dragonfly44.Tests;

public class ThemeServiceTests
{
    private class InMemorySettingsService : ISettingsService
    {
        private readonly Dictionary<string, object> _storage = new();
        public bool SaveAsyncCalled { get; private set; }

        public T Get<T>(string key, T defaultValue)
        {
            if (_storage.TryGetValue(key, out var val) && val is T typed) return typed;
            return defaultValue;
        }

        public void Set<T>(string key, T value)
        {
            if (value != null) _storage[key] = value;
        }

        public bool HasKey(string key) => _storage.ContainsKey(key);
        public void Remove(string key) => _storage.Remove(key);
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken = default)
        {
            SaveAsyncCalled = true;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task SetThemeAsync_ShouldUpdateCurrentThemeAndPersistToSettings()
    {
        var settings = new InMemorySettingsService();
        var themeService = new ThemeService(settings, NullLogger<ThemeService>.Instance);

        ApplicationThemePreference? changedTheme = null;
        themeService.ThemeChanged += (s, theme) => changedTheme = theme;

        // Act
        await themeService.SetThemeAsync(ApplicationThemePreference.Dark);

        // Assert
        themeService.CurrentTheme.Should().Be(ApplicationThemePreference.Dark);
        changedTheme.Should().Be(ApplicationThemePreference.Dark);
        settings.SaveAsyncCalled.Should().BeTrue("ThemeService must persist changes to disk via SaveAsync");
        settings.Get<string>("App.ThemePreference", "").Should().Be("Dark");
    }

    [Fact]
    public void NewInstance_ShouldRestoreThemeFromSettings()
    {
        var settings = new InMemorySettingsService();
        settings.Set("App.ThemePreference", "Light");

        // Act
        var themeService = new ThemeService(settings, NullLogger<ThemeService>.Instance);

        // Assert
        themeService.CurrentTheme.Should().Be(ApplicationThemePreference.Light);
    }
}
