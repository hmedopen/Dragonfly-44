using Dragonfly44.Application.Common.Models;
using Dragonfly44.Application.Interfaces;
using Dragonfly44.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dragonfly44.Presentation.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IScanEngineRegistry _scanEngineRegistry;
    private readonly IWindowStateService _windowStateService;
    private readonly INotificationService _notificationService;
    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;
    private readonly IKeyboardShortcutService _shortcutService;
    private readonly ILogger<MainViewModel> _logger;

    // Phase 1 properties
    private string _appTitle = "Dragonfly 44 — Custom UI Architecture";
    private string _statusMessage = "Initializing foundation services...";
    private string _databaseStatus = "Checking...";
    private string _databasePath = string.Empty;
    private string _diStatus = "Operational";
    private string _loggingStatus = "Active";
    private int _projectCount;
    private bool _isInitialized;

    // Phase 2 properties
    private string _windowBoundsText = "Loading...";
    private string _windowStateText = "Normal";
    private string _windowDpiText = "1.0x (96 DPI)";
    private string _shortcutStatusText = "Operational";
    private string _win2DStatusText = "Active (Direct2D GPU)";
    private string _lastActionMessage = "Ready. Test controls and shortcuts below.";
    private int _notificationCounter = 0;

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

    public string WindowBoundsText
    {
        get => _windowBoundsText;
        set => SetProperty(ref _windowBoundsText, value);
    }

    public string WindowStateText
    {
        get => _windowStateText;
        set => SetProperty(ref _windowStateText, value);
    }

    public string WindowDpiText
    {
        get => _windowDpiText;
        set => SetProperty(ref _windowDpiText, value);
    }

    public string ShortcutStatusText
    {
        get => _shortcutStatusText;
        set => SetProperty(ref _shortcutStatusText, value);
    }

    public string Win2DStatusText
    {
        get => _win2DStatusText;
        set => SetProperty(ref _win2DStatusText, value);
    }

    public string LastActionMessage
    {
        get => _lastActionMessage;
        set => SetProperty(ref _lastActionMessage, value);
    }

    public MainViewModel(
        IServiceScopeFactory scopeFactory,
        IScanEngineRegistry scanEngineRegistry,
        IWindowStateService windowStateService,
        INotificationService notificationService,
        ISettingsService settingsService,
        IDialogService dialogService,
        IKeyboardShortcutService shortcutService,
        ILogger<MainViewModel> logger)
    {
        _scopeFactory = scopeFactory;
        _scanEngineRegistry = scanEngineRegistry;
        _windowStateService = windowStateService;
        _notificationService = notificationService;
        _settingsService = settingsService;
        _dialogService = dialogService;
        _shortcutService = shortcutService;
        _logger = logger;

        _windowStateService.BoundsChanged += (s, b) =>
        {
            WindowBoundsText = $"{b.Width} x {b.Height} at ({b.X}, {b.Y})";
        };

        _windowStateService.StateChanged += (s, st) =>
        {
            WindowStateText = st.ToString();
        };

        _windowStateService.DpiChanged += (s, dpi) =>
        {
            WindowDpiText = $"{dpi:F2}x ({dpi * 96:F0} DPI)";
        };
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MainViewModel initialization started.");
        try
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var dbInitializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
                var projectService = scope.ServiceProvider.GetRequiredService<IProjectService>();

                DatabasePath = dbInitializer.GetDatabasePath();
                var canConnect = await dbInitializer.CanConnectAsync(cancellationToken);
                DatabaseStatus = canConnect ? "Connected (SQLite WAL Mode)" : "Connection Failed";

                var projects = await projectService.GetAllProjectsAsync(cancellationToken);
                ProjectCount = projects.Count;
            }

            var b = _windowStateService.CurrentBounds;
            WindowBoundsText = $"{b.Width} x {b.Height} at ({b.X}, {b.Y})";
            WindowStateText = _windowStateService.CurrentState.ToString();
            WindowDpiText = $"{_windowStateService.CurrentDpiScale:F2}x ({_windowStateService.CurrentDpiScale * 96:F0} DPI)";
            ShortcutStatusText = $"{_shortcutService.RegisteredShortcuts.Count} active shortcut(s)";

            StatusMessage = "Dragonfly 44 technical foundation and custom UI architecture verified.";
            IsInitialized = true;
            _logger.LogInformation("MainViewModel initialization complete.");
        }
        catch (Exception ex)
        {
            DatabaseStatus = $"Error: {ex.Message}";
            StatusMessage = "Foundation initialization encountered an error.";
            _logger.LogError(ex, "Error initializing MainViewModel");
        }
    }

    public void TriggerTestNotification()
    {
        _notificationCounter++;
        _notificationService.Show(
            $"Notification #{_notificationCounter} dispatched via centralized INotificationService.",
            "Technical Notification",
            Application.Common.Enums.NotificationSeverity.Information);

        LastActionMessage = $"Dispatched test notification #{_notificationCounter}";
    }

    public async Task TriggerTestDialogAsync()
    {
        LastActionMessage = "Awaiting modal dialog response...";
        var result = await _dialogService.ShowConfirmationAsync(
            "Dialog Service Verification",
            "This confirms the decoupled CustomDialogHost and IDialogService infrastructure is operational.",
            "Confirm",
            "Cancel");

        LastActionMessage = $"Dialog closed with result: {result}";
    }
}
