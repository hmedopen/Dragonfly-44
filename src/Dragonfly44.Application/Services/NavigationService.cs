using Dragonfly44.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Dragonfly44.Application.Services;

public class NavigationService : INavigationService
{
    private readonly Stack<string> _backStack = new();
    private readonly ILogger<NavigationService> _logger;

    public event EventHandler<string>? Navigated;

    public string? CurrentViewKey { get; private set; }
    public bool CanGoBack => _backStack.Count > 0;

    public NavigationService(ILogger<NavigationService> logger)
    {
        _logger = logger;
    }

    public bool NavigateTo(string viewKey, object? parameter = null)
    {
        if (string.IsNullOrWhiteSpace(viewKey)) return false;

        if (CurrentViewKey != null)
        {
            _backStack.Push(CurrentViewKey);
        }

        CurrentViewKey = viewKey;
        _logger.LogInformation("Navigated to view: '{ViewKey}' (CanGoBack: {CanGoBack})", viewKey, CanGoBack);
        Navigated?.Invoke(this, viewKey);
        return true;
    }

    public bool GoBack()
    {
        if (_backStack.Count == 0) return false;

        var previousView = _backStack.Pop();
        CurrentViewKey = previousView;
        _logger.LogInformation("Navigated back to view: '{ViewKey}'", previousView);
        Navigated?.Invoke(this, previousView);
        return true;
    }

    public void ClearHistory()
    {
        _backStack.Clear();
        _logger.LogDebug("Navigation history cleared");
    }
}
