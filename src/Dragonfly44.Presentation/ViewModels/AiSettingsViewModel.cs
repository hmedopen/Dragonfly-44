using System.Collections.ObjectModel;
using Dragonfly44.Application.AI.Interfaces;
using Dragonfly44.Application.AI.Models;
using Dragonfly44.Application.Interfaces;
using Dragonfly44.Application.Services;
using Microsoft.Extensions.Logging;

namespace Dragonfly44.Presentation.ViewModels;

/// <summary>
/// Presentation model managing AI translation provider configuration, model selection,
/// generation parameters, OS-backed secure credential storage, and live connectivity verification.
/// </summary>
public class AiSettingsViewModel : ViewModelBase
{
    private readonly IAiProviderRegistry _providerRegistry;
    private readonly ISecureCredentialStorage _credentialStorage;
    private readonly ISettingsService _settingsService;
    private readonly ITranslationService _translationService;
    private readonly ILogger<AiSettingsViewModel> _logger;

    private string _selectedProvider = "OpenAI";
    private AiModelInfo? _selectedModel;
    private string _apiKeyInput = string.Empty;
    private bool _hasSavedApiKey;
    private string _customEndpoint = string.Empty;
    private double _temperature = 0.3;
    private int _maxTokens = 1024;
    private bool _isTestingConnection;
    private string? _connectionTestResult;
    private bool _isConnectionSuccess;
    private string? _statusMessage;

    public ObservableCollection<string> AvailableProviders { get; } = new();
    public ObservableCollection<AiModelInfo> AvailableModels { get; } = new();

    public string SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            if (SetProperty(ref _selectedProvider, value))
            {
                _ = OnProviderChangedAsync(value);
            }
        }
    }

    public AiModelInfo? SelectedModel
    {
        get => _selectedModel;
        set => SetProperty(ref _selectedModel, value);
    }

    public string ApiKeyInput
    {
        get => _apiKeyInput;
        set => SetProperty(ref _apiKeyInput, value);
    }

    public bool HasSavedApiKey
    {
        get => _hasSavedApiKey;
        private set
        {
            if (SetProperty(ref _hasSavedApiKey, value))
            {
                OnPropertyChanged(nameof(ApiKeyStatusText));
                OnPropertyChanged(nameof(CanDeleteApiKey));
            }
        }
    }

    public string ApiKeyStatusText => HasSavedApiKey
        ? "✓ Stored securely in Windows DPAPI"
        : "⚠ No API key configured";

    public bool CanDeleteApiKey => HasSavedApiKey;

    public string CustomEndpoint
    {
        get => _customEndpoint;
        set => SetProperty(ref _customEndpoint, value);
    }

    public double Temperature
    {
        get => _temperature;
        set
        {
            if (SetProperty(ref _temperature, Math.Clamp(Math.Round(value, 2), 0.0, 1.0)))
            {
                OnPropertyChanged(nameof(TemperatureText));
            }
        }
    }

    public int MaxTokens
    {
        get => _maxTokens;
        set
        {
            if (SetProperty(ref _maxTokens, Math.Clamp(value, 64, 4096)))
            {
                OnPropertyChanged(nameof(MaxTokensText));
            }
        }
    }

    public string TemperatureText
    {
        get => _temperature.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        set
        {
            if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ||
                double.TryParse(value, out parsed))
            {
                Temperature = parsed;
            }
        }
    }

    public string MaxTokensText
    {
        get => _maxTokens.ToString();
        set
        {
            if (int.TryParse(value, out var parsed))
            {
                MaxTokens = parsed;
            }
        }
    }

    public bool IsTestingConnection
    {
        get => _isTestingConnection;
        set
        {
            if (SetProperty(ref _isTestingConnection, value))
            {
                OnPropertyChanged(nameof(CanTestConnection));
            }
        }
    }

    public bool CanTestConnection => !IsTestingConnection;

    public string? ConnectionTestResult
    {
        get => _connectionTestResult;
        set
        {
            if (SetProperty(ref _connectionTestResult, value))
            {
                OnPropertyChanged(nameof(HasConnectionTestResult));
            }
        }
    }

    public bool HasConnectionTestResult => !string.IsNullOrEmpty(ConnectionTestResult);

    public bool IsConnectionSuccess
    {
        get => _isConnectionSuccess;
        set => SetProperty(ref _isConnectionSuccess, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public AiSettingsViewModel(
        IAiProviderRegistry providerRegistry,
        ISecureCredentialStorage credentialStorage,
        ISettingsService settingsService,
        ITranslationService translationService,
        ILogger<AiSettingsViewModel> logger)
    {
        _providerRegistry = providerRegistry;
        _credentialStorage = credentialStorage;
        _settingsService = settingsService;
        _translationService = translationService;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        AvailableProviders.Clear();
        foreach (var provider in _providerRegistry.GetAllProviders())
        {
            AvailableProviders.Add(provider.ProviderName);
        }

        var savedProvider = _settingsService.Get("Ai:DefaultProvider", "OpenAI");
        if (AvailableProviders.Contains(savedProvider))
        {
            _selectedProvider = savedProvider;
            OnPropertyChanged(nameof(SelectedProvider));
        }
        else if (AvailableProviders.Count > 0)
        {
            _selectedProvider = AvailableProviders[0];
            OnPropertyChanged(nameof(SelectedProvider));
        }

        Temperature = _settingsService.Get($"Ai:{_selectedProvider}:Temperature", 0.3);
        MaxTokens = _settingsService.Get($"Ai:{_selectedProvider}:MaxTokens", 1024);
        CustomEndpoint = _settingsService.Get($"Ai:{_selectedProvider}:Endpoint", string.Empty);

        await OnProviderChangedAsync(_selectedProvider);
    }

    private async Task OnProviderChangedAsync(string providerName)
    {
        AvailableModels.Clear();
        var provider = _providerRegistry.GetProvider(providerName);
        if (provider != null)
        {
            foreach (var model in provider.SupportedModels)
            {
                AvailableModels.Add(model);
            }

            var savedModelId = _settingsService.Get($"Ai:{providerName}:DefaultModel", string.Empty);
            SelectedModel = AvailableModels.FirstOrDefault(m => m.Id == savedModelId)
                ?? AvailableModels.FirstOrDefault();
        }

        HasSavedApiKey = await _credentialStorage.HasCredentialAsync(providerName);
        ConnectionTestResult = null;
        StatusMessage = null;
    }

    public async Task TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        IsTestingConnection = true;
        ConnectionTestResult = "Testing connection to " + SelectedProvider + "...";
        IsConnectionSuccess = false;

        try
        {
            // If the user entered a key in the text field, save it before testing
            if (!string.IsNullOrWhiteSpace(ApiKeyInput))
            {
                await _credentialStorage.SetCredentialAsync(SelectedProvider, ApiKeyInput.Trim(), cancellationToken);
                HasSavedApiKey = true;
                ApiKeyInput = string.Empty;
            }

            var testResult = await _translationService.TestProviderConnectionAsync(
                SelectedProvider,
                SelectedModel?.Id,
                string.IsNullOrWhiteSpace(CustomEndpoint) ? null : CustomEndpoint.Trim(),
                cancellationToken);

            IsConnectionSuccess = testResult.IsSuccess;
            if (testResult.IsSuccess)
            {
                ConnectionTestResult = $"✓ Connected successfully ({testResult.LatencyMs}ms). Ready for translation.";
                StatusMessage = "Connection verified.";
            }
            else
            {
                ConnectionTestResult = $"✕ Connection failed: {testResult.Message}";
                StatusMessage = "Connection test failed.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during connection test for {Provider}", SelectedProvider);
            IsConnectionSuccess = false;
            ConnectionTestResult = $"✕ Error: {ex.Message}";
            StatusMessage = "Connection test encountered an error.";
        }
        finally
        {
            IsTestingConnection = false;
        }
    }

    public async Task SaveSettingsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(ApiKeyInput))
            {
                await _credentialStorage.SetCredentialAsync(SelectedProvider, ApiKeyInput.Trim(), cancellationToken);
                HasSavedApiKey = true;
                ApiKeyInput = string.Empty;
            }

            _settingsService.Set("Ai:DefaultProvider", SelectedProvider);
            if (SelectedModel != null)
            {
                _settingsService.Set($"Ai:{SelectedProvider}:DefaultModel", SelectedModel.Id);
            }
            _settingsService.Set($"Ai:{SelectedProvider}:Temperature", Temperature);
            _settingsService.Set($"Ai:{SelectedProvider}:MaxTokens", MaxTokens);
            _settingsService.Set($"Ai:{SelectedProvider}:Endpoint", CustomEndpoint?.Trim() ?? string.Empty);

            await _settingsService.SaveAsync(cancellationToken);
            StatusMessage = "Settings saved successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save AI settings");
            StatusMessage = $"Failed to save: {ex.Message}";
        }
    }

    public async Task DeleteApiKeyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _credentialStorage.DeleteCredentialAsync(SelectedProvider, cancellationToken);
            HasSavedApiKey = false;
            ApiKeyInput = string.Empty;
            ConnectionTestResult = null;
            StatusMessage = $"API key deleted for {SelectedProvider}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete API key for {Provider}", SelectedProvider);
            StatusMessage = $"Error deleting key: {ex.Message}";
        }
    }
}
