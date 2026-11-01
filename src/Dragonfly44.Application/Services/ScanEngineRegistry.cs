using System.Collections.Concurrent;
using Dragonfly44.Application.Interfaces;
using Dragonfly44.Domain.Contracts;
using Microsoft.Extensions.Logging;

namespace Dragonfly44.Application.Services;

public class ScanEngineRegistry : IScanEngineRegistry
{
    private readonly ConcurrentDictionary<string, IScanEngine> _engines = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<ScanEngineRegistry> _logger;

    public ScanEngineRegistry(ILogger<ScanEngineRegistry> logger)
    {
        _logger = logger;
    }

    public void Register(IScanEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        _engines[engine.ProviderId] = engine;
        _logger.LogInformation("Registered scan engine provider: {ProviderId}", engine.ProviderId);
    }

    public IScanEngine? GetProvider(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId)) return null;
        _engines.TryGetValue(providerId, out var engine);
        return engine;
    }

    public IReadOnlyList<string> GetAvailableProviders() =>
        _engines.Keys.OrderBy(k => k).ToList();
}
