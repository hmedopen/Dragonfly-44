using Dragonfly44.Application.Interfaces;
using Dragonfly44.Application.Services;
using Dragonfly44.Infrastructure.Configuration;
using Dragonfly44.Infrastructure.Exceptions;
using Dragonfly44.Infrastructure.Persistence;
using Dragonfly44.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        // 3. Application Services
        services.AddScoped<IProjectService, ProjectService>();
        services.AddSingleton<IScanEngineRegistry, ScanEngineRegistry>();

        // 4. Exception Handling
        services.AddSingleton<GlobalExceptionHandler>();

        return services;
    }
}
