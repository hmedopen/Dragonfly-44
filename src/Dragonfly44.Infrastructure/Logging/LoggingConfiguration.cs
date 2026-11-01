using Microsoft.Extensions.Logging;

namespace Dragonfly44.Infrastructure.Logging;

public static class LoggingConfiguration
{
    public static ILoggingBuilder AddDragonflyFileLogging(this ILoggingBuilder builder, string logFilePath)
    {
        builder.AddProvider(new AppFileLoggerProvider(logFilePath));
        return builder;
    }
}
