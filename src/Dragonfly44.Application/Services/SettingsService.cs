using System.Text.Json;
using Dragonfly44.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Dragonfly44.Application.Services;

public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private readonly ILogger<SettingsService> _logger;
    private readonly Dictionary<string, JsonElement> _settings = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public SettingsService(ILogger<SettingsService> logger, string? settingsFilePath = null)
    {
        _logger = logger;
        if (!string.IsNullOrEmpty(settingsFilePath))
        {
            _settingsFilePath = settingsFilePath;
        }
        else
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var settingsDir = Path.Combine(localAppData, "Dragonfly44");
            _settingsFilePath = Path.Combine(settingsDir, "settings.json");
        }
    }

    public T Get<T>(string key, T defaultValue)
    {
        lock (_settings)
        {
            if (_settings.TryGetValue(key, out var element))
            {
                try
                {
                    var val = JsonSerializer.Deserialize<T>(element.GetRawText());
                    return val ?? defaultValue;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize setting '{Key}'", key);
                }
            }
        }
        return defaultValue;
    }

    public void Set<T>(string key, T value)
    {
        var json = JsonSerializer.SerializeToElement(value);
        lock (_settings)
        {
            _settings[key] = json;
        }
    }

    public bool HasKey(string key)
    {
        lock (_settings)
        {
            return _settings.ContainsKey(key);
        }
    }

    public void Remove(string key)
    {
        lock (_settings)
        {
            _settings.Remove(key);
        }
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                _logger.LogInformation("No settings file found at {Path}. Using defaults.", _settingsFilePath);
                return;
            }

            var json = await File.ReadAllTextAsync(_settingsFilePath, cancellationToken);
            var loaded = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (loaded != null)
            {
                lock (_settings)
                {
                    _settings.Clear();
                    foreach (var kvp in loaded)
                    {
                        _settings[kvp.Key] = kvp.Value;
                    }
                }
                _logger.LogInformation("Loaded {Count} settings from {Path}", _settings.Count, _settingsFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings from {Path}", _settingsFilePath);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        string? tempFilePath = null;
        try
        {
            var dir = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            Dictionary<string, JsonElement> copy;
            lock (_settings)
            {
                copy = new Dictionary<string, JsonElement>(_settings);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(copy, options);

            // Write through a unique temporary file in the same directory
            tempFilePath = Path.Combine(dir ?? string.Empty, $"settings_{Guid.NewGuid():N}.tmp");

            await File.WriteAllTextAsync(tempFilePath, json, cancellationToken);

            // Safe atomic replacement preserving existing file on failure
            File.Move(tempFilePath, _settingsFilePath, overwrite: true);
            tempFilePath = null; // Successfully moved

            _logger.LogDebug("Atomically saved settings to {Path}", _settingsFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings to {Path}", _settingsFilePath);

            // Clean up temporary file if write or replace failed
            if (tempFilePath != null && File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch
                {
                    // Ignore temp file cleanup errors
                }
            }

            // Propagate exception so callers can observe save failures
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
