using Dragonfly44.Infrastructure.Exceptions;
using Dragonfly44.Infrastructure.Logging;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Dragonfly44.Tests;

public class LoggingAndExceptionTests : IDisposable
{
    private readonly string _testLogPath;

    public LoggingAndExceptionTests()
    {
        _testLogPath = Path.Combine(Path.GetTempPath(), $"dragonfly44_test_log_{Guid.NewGuid():N}.log");
    }

    [Fact]
    public async Task AppFileLogger_ShouldWriteLogEntriesToFile()
    {
        // Arrange
        using (var provider = new AppFileLoggerProvider(_testLogPath))
        {
            var logger = provider.CreateLogger("TestCategory");

            // Act
            logger.LogInformation("Diagnostic message for testing file logging.");
            logger.LogWarning("Diagnostic warning entry.");

            // Give queue brief moment to flush
            await Task.Delay(200);
        }

        // Assert
        File.Exists(_testLogPath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(_testLogPath);
        content.Should().Contain("Diagnostic message for testing file logging.");
        content.Should().Contain("Diagnostic warning entry.");
        content.Should().Contain("[TestCategory]");
    }

    [Fact]
    public void GlobalExceptionHandler_ShouldTriggerNotificationEvent()
    {
        // Arrange
        using var loggerFactory = LoggerFactory.Create(b => b.AddDebug());
        var logger = loggerFactory.CreateLogger<GlobalExceptionHandler>();
        var handler = new GlobalExceptionHandler(logger);

        Exception? capturedException = null;
        string? capturedSource = null;

        handler.UnhandledExceptionOccurred += (ex, source) =>
        {
            capturedException = ex;
            capturedSource = source;
        };

        // Act
        var testEx = new InvalidOperationException("Simulated test error");
        handler.HandleException(testEx, "UnitTestContext", isFatal: false);

        // Assert
        capturedException.Should().BeSameAs(testEx);
        capturedSource.Should().Be("UnitTestContext");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_testLogPath)) File.Delete(_testLogPath);
        }
        catch { }
    }
}
