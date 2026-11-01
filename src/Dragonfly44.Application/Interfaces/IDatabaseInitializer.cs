namespace Dragonfly44.Application.Interfaces;

public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
    string GetDatabasePath();
}
