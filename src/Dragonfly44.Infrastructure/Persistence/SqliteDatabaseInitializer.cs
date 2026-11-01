using Dragonfly44.Application.Interfaces;
using Dragonfly44.Infrastructure.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dragonfly44.Infrastructure.Persistence;

public class SqliteDatabaseInitializer : IDatabaseInitializer
{
    private const string InitialMigrationId = "20261101213306_InitialCreate";
    private const string EfProductVersion = "8.0.10";

    private readonly Dragonfly44DbContext _dbContext;
    private readonly DatabaseOptions _databaseOptions;
    private readonly StorageOptions _storageOptions;
    private readonly ILogger<SqliteDatabaseInitializer> _logger;

    public SqliteDatabaseInitializer(
        Dragonfly44DbContext dbContext,
        IOptions<DatabaseOptions> databaseOptions,
        IOptions<StorageOptions> storageOptions,
        ILogger<SqliteDatabaseInitializer> logger)
    {
        _dbContext = dbContext;
        _databaseOptions = databaseOptions.Value;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var dbPath = GetDatabasePath();
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            _logger.LogInformation("Created database storage directory at: {Directory}", directory);
        }

        _logger.LogInformation("Initializing SQLite database at: {DbPath}", dbPath);

        // Check for legacy EnsureCreated database before running migrations
        await HandleLegacyEnsureCreatedTransitionAsync(cancellationToken);

        // Apply any pending EF Core migrations
        _logger.LogInformation("Applying EF Core migrations...");
        await _dbContext.Database.MigrateAsync(cancellationToken);

        // Enable SQLite WAL mode and foreign keys for desktop performance & integrity
        await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL;", cancellationToken);
        await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;", cancellationToken);

        _logger.LogInformation("Database initialized and migrated successfully with WAL mode and foreign key enforcement.");
    }

    private async Task HandleLegacyEnsureCreatedTransitionAsync(CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var wasClosed = connection.State == System.Data.ConnectionState.Closed;
        if (wasClosed)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            // Check if domain tables exist (e.g. Projects)
            using var tableCmd = connection.CreateCommand();
            tableCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Projects';";
            var projectsTableCount = Convert.ToInt64(await tableCmd.ExecuteScalarAsync(cancellationToken));

            // Check if __EFMigrationsHistory table exists
            using var historyCmd = connection.CreateCommand();
            historyCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory';";
            var historyTableCount = Convert.ToInt64(await historyCmd.ExecuteScalarAsync(cancellationToken));

            if (projectsTableCount > 0 && historyTableCount == 0)
            {
                _logger.LogInformation("Detected existing database created with EnsureCreated. Bootstrapping __EFMigrationsHistory to preserve existing data...");

                using var createHistoryCmd = connection.CreateCommand();
                createHistoryCmd.CommandText = $@"
                    CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
                        ""MigrationId"" TEXT NOT NULL CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY,
                        ""ProductVersion"" TEXT NOT NULL
                    );
                    INSERT OR IGNORE INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                    VALUES ('{InitialMigrationId}', '{EfProductVersion}');
                ";
                await createHistoryCmd.ExecuteNonQueryAsync(cancellationToken);
                _logger.LogInformation("Successfully bootstrapped initial migration into __EFMigrationsHistory. Existing data preserved.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check or bootstrap legacy migration history table. Proceeding with standard migration.");
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Database.CanConnectAsync(cancellationToken);
    }

    public string GetDatabasePath()
    {
        var connectionString = _dbContext.Database.GetConnectionString();
        if (string.IsNullOrEmpty(connectionString))
        {
            return Path.Combine(_storageOptions.AppDataDirectory, _databaseOptions.DatabaseFileName ?? "dragonfly44.db");
        }

        try
        {
            var builder = new SqliteConnectionStringBuilder(connectionString);
            return Path.GetFullPath(builder.DataSource);
        }
        catch
        {
            return connectionString;
        }
    }
}
