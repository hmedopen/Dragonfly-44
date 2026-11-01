using Dragonfly44.Application.Interfaces;
using Dragonfly44.Infrastructure.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dragonfly44.Infrastructure.Persistence;

public class SqliteDatabaseInitializer : IDatabaseInitializer
{
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

        // Ensure schema is created / migrated
        await _dbContext.Database.EnsureCreatedAsync(cancellationToken);

        // Enable SQLite WAL mode and foreign keys for desktop performance & integrity
        await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL;", cancellationToken);
        await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;", cancellationToken);

        _logger.LogInformation("Database initialized successfully with WAL mode and foreign key enforcement.");
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
