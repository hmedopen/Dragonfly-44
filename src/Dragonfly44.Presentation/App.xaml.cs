using Dragonfly44.Application.Interfaces;
using Dragonfly44.Application.Services;
using Dragonfly44.Infrastructure.DependencyInjection;
using Dragonfly44.Infrastructure.Exceptions;
using Dragonfly44.Infrastructure.Logging;
using Dragonfly44.Presentation.Input;
using Dragonfly44.Presentation.Rendering;
using Dragonfly44.Presentation.ViewModels;
using Dragonfly44.Presentation.Windowing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace Dragonfly44.Presentation;

public partial class App : Microsoft.UI.Xaml.Application
{
    private readonly IHost _host;
    private Window? _mainWindow;

    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        InitializeComponent();

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var logsDir = Path.Combine(localAppData, "Dragonfly44", "logs");
        var logFilePath = Path.Combine(logsDir, "dragonfly44.log");

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables("DRAGONFLY44_");
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddConfiguration(context.Configuration.GetSection("Logging"));
                logging.AddDebug();
                logging.AddConsole();
                logging.AddDragonflyFileLogging(logFilePath);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddDragonflyInfrastructure(context.Configuration);

                // Application & Window Services (Phase 2)
                services.AddSingleton<ISettingsService, SettingsService>();
                services.AddSingleton<IWindowStateService, WindowStateService>();
                services.AddSingleton<INotificationService, NotificationService>();
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<IThemeService, ThemeService>();

                // Input & Interaction Services (Phase 2)
                services.AddSingleton<IKeyboardShortcutService, KeyboardShortcutService>();
                services.AddSingleton<IFocusService, FocusService>();
                services.AddSingleton<IClipboardService, ClipboardService>();
                services.AddSingleton<IDragDropService, DragDropCoordinator>();
                services.AddSingleton<InputRouter>();

                // Rendering & Windowing Primitives (Phase 2)
                services.AddTransient<IWindowController, WindowController>();
                services.AddTransient<VectorRenderer>();

                // ViewModels & Windows
                services.AddTransient<MainViewModel>();
                services.AddTransient<MainWindow>();
            })
            .Build();

        Services = _host.Services;

        // Global Exception Handling Setup
        SetupGlobalExceptionHandling();
    }

    private void SetupGlobalExceptionHandling()
    {
        var exceptionHandler = Services.GetRequiredService<GlobalExceptionHandler>();

        // 1. WinUI 3 UnhandledException
        UnhandledException += (sender, e) =>
        {
            exceptionHandler.HandleException(e.Exception, "WinUI.App.UnhandledException", isFatal: false);
            e.Handled = true; // Prevent crash when possible
        };

        // 2. AppDomain UnhandledException
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                exceptionHandler.HandleException(ex, "AppDomain.CurrentDomain.UnhandledException", e.IsTerminating);
            }
        };

        // 3. TaskScheduler UnobservedTaskException
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            exceptionHandler.HandleException(e.Exception, "TaskScheduler.UnobservedTaskException", isFatal: false);
            e.SetObserved();
        };
    }

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var logger = Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("Dragonfly 44 is launching. Initializing foundation...");

        try
        {
            // Load application settings
            var settingsService = Services.GetRequiredService<ISettingsService>();
            await settingsService.LoadAsync();

            // Initialize Database using an explicit DI scope
            using (var scope = Services.CreateScope())
            {
                var dbInitializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
                await dbInitializer.InitializeAsync();
            }

            _mainWindow = Services.GetRequiredService<MainWindow>();
            _mainWindow.Activate();

            if (_mainWindow.Content is FrameworkElement root && root.DataContext is MainViewModel vm)
            {
                await vm.InitializeAsync();
            }

            logger.LogInformation("Dragonfly 44 foundation window activated successfully.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Fatal error during application launch");
            throw;
        }
    }
}
