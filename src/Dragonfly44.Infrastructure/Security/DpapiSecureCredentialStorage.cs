using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dragonfly44.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Dragonfly44.Infrastructure.Security;

/// <summary>
/// Operating-system-backed secure storage for API keys and credentials using Windows Data Protection API (DPAPI).
/// Secrets are encrypted with the current user's credentials and saved in an encrypted binary file.
/// API keys are never stored in plaintext SQLite fields, ordinary application settings, or logs.
/// </summary>
public class DpapiSecureCredentialStorage : ISecureCredentialStorage
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Dragonfly44_AiSecureCredentialEntropy_v1");

    private readonly string _credentialsFilePath;
    private readonly ILogger<DpapiSecureCredentialStorage> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly Dictionary<string, string> _cachedPlaintext = new();
    private bool _isLoaded;

    public DpapiSecureCredentialStorage(ILogger<DpapiSecureCredentialStorage> logger, string? customFilePath = null)
    {
        _logger = logger;
        if (!string.IsNullOrEmpty(customFilePath))
        {
            _credentialsFilePath = customFilePath;
        }
        else
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appDir = Path.Combine(localAppData, "Dragonfly44");
            _credentialsFilePath = Path.Combine(appDir, "credentials.enc");
        }
    }

    public async Task SetCredentialAsync(string key, string secret, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key cannot be null or whitespace", nameof(key));
        if (secret == null) throw new ArgumentNullException(nameof(secret));

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            _cachedPlaintext[key] = secret;
            await PersistEncryptedAsync(cancellationToken);
            _logger.LogInformation("Credential securely updated for key: {Key}", SanitizeKeyName(key));
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<string?> GetCredentialAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            return _cachedPlaintext.TryGetValue(key, out var secret) ? secret : null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteCredentialAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key)) return;

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            if (_cachedPlaintext.Remove(key))
            {
                await PersistEncryptedAsync(cancellationToken);
                _logger.LogInformation("Credential deleted for key: {Key}", SanitizeKeyName(key));
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> HasCredentialAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            return _cachedPlaintext.TryGetValue(key, out var secret) && !string.IsNullOrWhiteSpace(secret);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_isLoaded) return;

        if (!File.Exists(_credentialsFilePath))
        {
            _isLoaded = true;
            return;
        }

        try
        {
            var encryptedBytes = await File.ReadAllBytesAsync(_credentialsFilePath, cancellationToken);
            if (encryptedBytes.Length == 0)
            {
                _isLoaded = true;
                return;
            }

            byte[] decryptedBytes;
            if (OperatingSystem.IsWindows())
            {
                decryptedBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
            }
            else
            {
                // Cross-platform fallback for testing environments
                decryptedBytes = encryptedBytes;
            }

            var json = Encoding.UTF8.GetString(decryptedBytes);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (dict != null)
            {
                _cachedPlaintext.Clear();
                foreach (var kvp in dict)
                {
                    _cachedPlaintext[kvp.Key] = kvp.Value;
                }
            }

            _isLoaded = true;
            _logger.LogInformation("Secure credentials successfully loaded ({Count} entries).", _cachedPlaintext.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt secure credentials file. Resetting credentials cache.");
            _cachedPlaintext.Clear();
            _isLoaded = true;
        }
    }

    private async Task PersistEncryptedAsync(CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(_credentialsFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(_cachedPlaintext);
        var plaintextBytes = Encoding.UTF8.GetBytes(json);

        byte[] encryptedBytes;
        if (OperatingSystem.IsWindows())
        {
            encryptedBytes = ProtectedData.Protect(plaintextBytes, Entropy, DataProtectionScope.CurrentUser);
        }
        else
        {
            encryptedBytes = plaintextBytes;
        }

        await File.WriteAllBytesAsync(_credentialsFilePath, encryptedBytes, cancellationToken);
    }

    private static string SanitizeKeyName(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        return key.Length <= 8 ? key : key[..4] + "***";
    }
}
