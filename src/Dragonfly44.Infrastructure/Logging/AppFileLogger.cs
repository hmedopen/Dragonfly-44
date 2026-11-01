using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Dragonfly44.Infrastructure.Logging;

public class AppFileLoggerProvider : ILoggerProvider
{
    private readonly string _logFilePath;
    private readonly ConcurrentDictionary<string, AppFileLogger> _loggers = new();
    private readonly BlockingCollection<string> _messageQueue = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _outputTask;

    public AppFileLoggerProvider(string logFilePath)
    {
        _logFilePath = logFilePath;
        var dir = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _outputTask = Task.Run(ProcessQueueAsync);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new AppFileLogger(name, _messageQueue));
    }

    private async Task ProcessQueueAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                if (_messageQueue.TryTake(out var message, 100, _cts.Token))
                {
                    await File.AppendAllTextAsync(_logFilePath, message + Environment.NewLine, Encoding.UTF8, _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Prevent crashing logging background worker
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _outputTask.Wait(1000);
            while (_messageQueue.TryTake(out var message))
            {
                File.AppendAllText(_logFilePath, message + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch { }
        _cts.Dispose();
        _messageQueue.Dispose();
    }
}

public class AppFileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly BlockingCollection<string> _queue;

    public AppFileLogger(string categoryName, BlockingCollection<string> queue)
    {
        _categoryName = categoryName;
        _queue = queue;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logLine = $"[{timestamp}] [{logLevel,-11}] [{_categoryName}] {message}";

        if (exception != null)
        {
            logLine += Environment.NewLine + exception;
        }

        try
        {
            _queue.Add(logLine);
        }
        catch
        {
            // Logging failure shouldn't crash app
        }
    }
}
