using Dragonfly44.Application.AI.Interfaces;
using Dragonfly44.Application.Interfaces;
using Dragonfly44.Application.Services;
using Dragonfly44.Application.Synchronization;
using Dragonfly44.Infrastructure.AI.Providers;
using Dragonfly44.Infrastructure.AI.Services;
using Dragonfly44.Infrastructure.Configuration;
using Dragonfly44.Infrastructure.Exceptions;
using Dragonfly44.Infrastructure.Imaging;
using Dragonfly44.Infrastructure.Persistence;
using Dragonfly44.Infrastructure.Persistence.Repositories;
using Dragonfly44.Infrastructure.Security;
using Dragonfly44.Infrastructure.Services;
using Dragonfly44.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dragonfly44.Infrastructure.DependencyInjection;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddDragonflyInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string? overrideDbPath = null)
    {
        // 1. Configuration options
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var defaultAppData = Path.Combine(localAppData, "Dragonfly44");
        var defaultLogs = Path.Combine(defaultAppData, "logs");
        var defaultProjects = Path.Combine(defaultAppData, "projects");

        services.Configure<StorageOptions>(options =>
        {
            configuration.GetSection(StorageOptions.SectionName).Bind(options);
            if (string.IsNullOrEmpty(options.AppDataDirectory)) options.AppDataDirectory = defaultAppData;
            if (string.IsNullOrEmpty(options.LogsDirectory)) options.LogsDirectory = defaultLogs;
            if (string.IsNullOrEmpty(options.ProjectsRootDirectory)) options.ProjectsRootDirectory = defaultProjects;
        });

        services.Configure<DatabaseOptions>(options =>
        {
            configuration.GetSection(DatabaseOptions.SectionName).Bind(options);
        });

        // 2. Persistence / SQLite
        services.AddDbContext<Dragonfly44DbContext>((serviceProvider, options) =>
        {
            string connectionString;
            if (!string.IsNullOrEmpty(overrideDbPath))
            {
                connectionString = overrideDbPath.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
                    ? overrideDbPath
                    : $"Data Source={overrideDbPath}";
            }
            else
            {
                var configuredConn = configuration.GetConnectionString("DefaultConnection");
                if (!string.IsNullOrEmpty(configuredConn))
                {
                    connectionString = configuredConn;
                }
                else
                {
                    var dbFile = Path.Combine(defaultAppData, "dragonfly44.db");
                    connectionString = $"Data Source={dbFile}";
                }
            }

            options.UseSqlite(connectionString, sqliteOptions =>
            {
                sqliteOptions.MigrationsAssembly(typeof(Dragonfly44DbContext).Assembly.FullName);
            });
        });

        services.AddScoped<IDatabaseInitializer, SqliteDatabaseInitializer>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IChapterRepository, ChapterRepository>();
        services.AddScoped<IPageRepository, PageRepository>();
        services.AddScoped<IDraftRepository, DraftRepository>();
        services.AddScoped<IBubbleRepository, BubbleRepository>();

        // 3. Storage & Imaging
        services.AddSingleton<IStorageService, FileSystemStorageService>();
        services.AddSingleton<IImageMetadataReader, ImageMetadataReader>();

        // 4. Application Services
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IChapterService, ChapterService>();
        services.AddScoped<IDialogueService, DialogueService>();
        services.AddScoped<IMangaIngestionService, MangaIngestionService>();
        services.AddScoped<IDraftService, DraftService>();
        services.AddSingleton<IScanEngineRegistry, ScanEngineRegistry>();
        services.AddSingleton<IWorkspaceSyncCoordinator, WorkspaceSyncCoordinator>();

        // 5. Exception Handling
        services.AddSingleton<GlobalExceptionHandler>();

        // 6. AI Translation Engine (Phase 7)
        services.AddHttpClient();
        services.AddSingleton<ISecureCredentialStorage, DpapiSecureCredentialStorage>();
        services.AddSingleton<ITranslationPromptBuilder, TranslationPromptBuilder>();
        services.AddSingleton<ICostCalculator, ModelCostCalculator>();
        services.AddSingleton<IAiProviderRegistry>(sp =>
        {
            var registry = new AiProviderRegistry();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();

            var openAiClient = httpClientFactory.CreateClient("OpenAiClient");
            var openAiLogger = loggerFactory.CreateLogger<OpenAiTranslationProvider>();
            registry.RegisterProvider(new OpenAiTranslationProvider(openAiClient, openAiLogger));

            var anthropicClient = httpClientFactory.CreateClient("AnthropicClient");
            var anthropicLogger = loggerFactory.CreateLogger<AnthropicTranslationProvider>();
            registry.RegisterProvider(new AnthropicTranslationProvider(anthropicClient, anthropicLogger));

            return registry;
        });
        services.AddScoped<ITranslationService, TranslationService>();

        return services;
    }
}
