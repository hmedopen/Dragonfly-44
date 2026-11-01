using Microsoft.Extensions.Logging;

namespace Dragonfly44.Infrastructure.Exceptions;

public class GlobalExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public event Action<Exception, string>? UnhandledExceptionOccurred;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public void HandleException(Exception exception, string contextSource, bool isFatal = false)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (isFatal)
        {
            _logger.LogCritical(exception, "[FATAL UNHANDLED EXCEPTION] Source: {ContextSource}. Application may terminate.", contextSource);
        }
        else
        {
            _logger.LogError(exception, "[UNHANDLED EXCEPTION] Source: {ContextSource}.", contextSource);
        }

        try
        {
            UnhandledExceptionOccurred?.Invoke(exception, contextSource);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while notifying subscribers of unhandled exception from {ContextSource}.", contextSource);
        }
    }
}
