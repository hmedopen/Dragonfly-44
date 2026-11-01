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

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var settingsDir = Path.Combine(localAppData, "Dragonfly44");
        Directory.CreateDirectory(settingsDir);
        _settingsFilePath = Path.Combine(settingsDir, "settings.json");
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
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            Dictionary<string, JsonElement> copy;
            lock (_settings)
            {
                copy = new Dictionary<string, JsonElement>(_settings);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(copy, options);
            await File.WriteAllTextAsync(_settingsFilePath, json, cancellationToken);
            _logger.LogDebug("Saved settings to {Path}", _settingsFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings to {Path}", _settingsFilePath);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
