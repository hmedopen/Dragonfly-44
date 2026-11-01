namespace Dragonfly44.Application.Interfaces;

public enum ApplicationThemePreference
{
    System,
    Dark,
    Light
}

public interface IThemeService
{
    event EventHandler<ApplicationThemePreference> ThemeChanged;

    ApplicationThemePreference CurrentTheme { get; }

    void SetTheme(ApplicationThemePreference theme);
    Task SetThemeAsync(ApplicationThemePreference theme);
}
