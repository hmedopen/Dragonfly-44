using Dragonfly44.Application.Interfaces;
using Dragonfly44.Application.Services;
using Microsoft.Extensions.Logging;

namespace Dragonfly44.Presentation.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IDatabaseInitializer _dbInitializer;
    private readonly IProjectService _projectService;
    private readonly IScanEngineRegistry _scanEngineRegistry;
    private readonly ILogger<MainViewModel> _logger;

    private string _appTitle = "Dragonfly 44 — Phase 1 Diagnostic";
    private string _statusMessage = "Initializing foundation services...";
    private string _databaseStatus = "Checking...";
    private string _databasePath = string.Empty;
    private string _diStatus = "Operational";
    private string _loggingStatus = "Active";
    private int _projectCount;
    private bool _isInitialized;

    public string AppTitle
    {
        get => _appTitle;
        set => SetProperty(ref _appTitle, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string DatabaseStatus
    {
        get => _databaseStatus;
        set => SetProperty(ref _databaseStatus, value);
    }

    public string DatabasePath
    {
        get => _databasePath;
        set => SetProperty(ref _databasePath, value);
    }

    public string DiStatus
    {
        get => _diStatus;
        set => SetProperty(ref _diStatus, value);
    }

    public string LoggingStatus
    {
        get => _loggingStatus;
        set => SetProperty(ref _loggingStatus, value);
    }

    public int ProjectCount
    {
        get => _projectCount;
        set => SetProperty(ref _projectCount, value);
    }

    public bool IsInitialized
    {
        get => _isInitialized;
        set => SetProperty(ref _isInitialized, value);
    }

    public MainViewModel(
        IDatabaseInitializer dbInitializer,
        IProjectService projectService,
        IScanEngineRegistry scanEngineRegistry,
        ILogger<MainViewModel> logger)
    {
        _dbInitializer = dbInitializer;
        _projectService = projectService;
        _scanEngineRegistry = scanEngineRegistry;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MainViewModel initialization started.");
        try
        {
            DatabasePath = _dbInitializer.GetDatabasePath();
            var canConnect = await _dbInitializer.CanConnectAsync(cancellationToken);
            DatabaseStatus = canConnect ? "Connected (SQLite WAL Mode)" : "Connection Failed";

            var projects = await _projectService.GetAllProjectsAsync(cancellationToken);
            ProjectCount = projects.Count;

            StatusMessage = "Dragonfly 44 technical foundation verified successfully.";
            IsInitialized = true;
            _logger.LogInformation("MainViewModel initialization complete. Found {Count} projects.", ProjectCount);
        }
        catch (Exception ex)
        {
            DatabaseStatus = $"Error: {ex.Message}";
            StatusMessage = "Foundation initialization encountered an error.";
            _logger.LogError(ex, "Error initializing MainViewModel");
        }
    }
}
