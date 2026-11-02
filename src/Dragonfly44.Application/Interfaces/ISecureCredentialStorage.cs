namespace Dragonfly44.Application.Interfaces;

/// <summary>
/// Operating-system-backed secure storage for sensitive API credentials.
/// Credentials are encrypted at rest and never written to plaintext SQLite or application logs.
/// </summary>
public interface ISecureCredentialStorage
{
    Task SetCredentialAsync(string key, string secret, CancellationToken cancellationToken = default);
    Task<string?> GetCredentialAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteCredentialAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> HasCredentialAsync(string key, CancellationToken cancellationToken = default);
}
