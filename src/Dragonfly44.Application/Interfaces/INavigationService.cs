namespace Dragonfly44.Application.Interfaces;

public interface INavigationService
{
    event EventHandler<string> Navigated;

    string? CurrentViewKey { get; }
    bool CanGoBack { get; }

    bool NavigateTo(string viewKey, object? parameter = null);
    bool GoBack();
    void ClearHistory();
}
