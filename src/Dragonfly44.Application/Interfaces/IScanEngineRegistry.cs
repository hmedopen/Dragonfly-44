using Dragonfly44.Domain.Contracts;

namespace Dragonfly44.Application.Interfaces;

public interface IScanEngineRegistry
{
    void Register(IScanEngine engine);
    IScanEngine? GetProvider(string providerId);
    IReadOnlyList<string> GetAvailableProviders();
}
