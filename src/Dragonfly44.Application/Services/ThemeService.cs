using Dragonfly44.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Dragonfly44.Application.Services;

public class ThemeService : IThemeService
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<ThemeService> _logger;
    private const string ThemeSettingKey = "App.ThemePreference";

    public ApplicationThemePreference CurrentTheme { get; private set; } = ApplicationThemePreference.System;

    public event EventHandler<ApplicationThemePreference>? ThemeChanged;

    public ThemeService(
        ISettingsService settingsService,
        ILogger<ThemeService> logger)
    {
        _settingsService = settingsService;
        _logger = logger;

        var savedTheme = _settingsService.Get(ThemeSettingKey, "System");
        if (Enum.TryParse<ApplicationThemePreference>(savedTheme, out var parsed))
        {
            CurrentTheme = parsed;
        }
    }

    public void SetTheme(ApplicationThemePreference theme)
    {
        _ = SetThemeAsync(theme);
    }

    public async Task SetThemeAsync(ApplicationThemePreference theme)
    {
        CurrentTheme = theme;
        _settingsService.Set(ThemeSettingKey, theme.ToString());
        await _settingsService.SaveAsync();

        _logger.LogInformation("Theme preference changed and persisted to: {Theme}", theme);
        ThemeChanged?.Invoke(this, theme);
    }
}
