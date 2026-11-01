using System.Data.Common;
using Dragonfly44.Application.Interfaces;
using Dragonfly44.Infrastructure.Configuration;
using Dragonfly44.Infrastructure.Exceptions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dragonfly44.Infrastructure.Persistence;

public class SqliteDatabaseInitializer : IDatabaseInitializer
{
    private const string InitialMigrationId = "20261101213306_InitialCreate";
    private const string EfProductVersion = "8.0.10";

    private static readonly Dictionary<string, HashSet<string>> ExpectedLegacySchema = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Projects"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Id", "Name", "SourceLanguage", "TargetLanguage", "Description", "RootDirectory", "CreatedAtUtc", "UpdatedAtUtc"
        },
        ["Chapters"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Id", "ProjectId", "ChapterNumber", "Title", "Status", "CreatedAtUtc", "UpdatedAtUtc"
        },
        ["Characters"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Id", "ProjectId", "CanonicalName", "LocalizedName", "Description", "ColorHex", "Notes", "CreatedAtUtc"
        },
        ["GlossaryEntries"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Id", "ProjectId", "SourceTerm", "TargetTerm", "Category", "ContextNotes", "CreatedAtUtc"
        },
        ["ChapterContexts"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Id", "ChapterId", "Summary", "ToneGuidelines", "CulturalNotes", "ConsistencyRules", "UpdatedAtUtc"
        },
        ["Pages"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Id", "ChapterId", "PageNumber", "ImageRelativePath", "Width", "Height", "Status", "CreatedAtUtc"
        },
        ["Bubbles"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "Id", "PageId", "CharacterId", "X", "Y", "Width", "Height", "RawText", "TranslatedText", "ReadingOrderIndex", "Confidence", "ShapeType", "CreatedAtUtc"
        }
    };

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

        // Step 1: Create a safety backup if an existing database file is present on disk
        string? backupPath = null;
        if (IsPhysicalDatabase(dbPath) && File.Exists(dbPath) && new FileInfo(dbPath).Length > 0)
        {
            backupPath = await CreateBackupAsync(dbPath, cancellationToken);
        }

        // Step 2: Validate existing schema and handle legacy EnsureCreated transition safely
        await HandleLegacyEnsureCreatedTransitionAsync(backupPath, cancellationToken);

        // Step 3: Apply any pending EF Core migrations
        _logger.LogInformation("Applying EF Core migrations...");
        await _dbContext.Database.MigrateAsync(cancellationToken);

        // Step 4: Enable SQLite WAL mode and foreign keys for desktop performance & integrity
        await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL;", cancellationToken);
        await _dbContext.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys = ON;", cancellationToken);

        _logger.LogInformation("Database initialized and migrated successfully with WAL mode and foreign key enforcement.");
    }

    private static bool IsPhysicalDatabase(string path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && !path.Equals(":memory:", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("mode=memory", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> CreateBackupAsync(string dbPath, CancellationToken cancellationToken)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var shortGuid = Guid.NewGuid().ToString("N")[..8];
        var backupPath = $"{dbPath}.bak_{timestamp}_{shortGuid}";

        _logger.LogInformation("Creating safety backup of database before migration: {BackupPath}", backupPath);

        try
        {
            var connection = _dbContext.Database.GetDbConnection();
            var wasClosed = connection.State == System.Data.ConnectionState.Closed;
            if (wasClosed)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                // VACUUM INTO creates an atomic, standalone, consistent copy of the database
                using var cmd = connection.CreateCommand();
                cmd.CommandText = $"VACUUM INTO '{backupPath.Replace("'", "''")}';";
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            finally
            {
                if (wasClosed)
                {
                    await connection.CloseAsync();
                }
            }

            _logger.LogInformation("Database safety backup created successfully at: {BackupPath}", backupPath);
            return backupPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create atomic database backup using VACUUM INTO. Attempting file copy fallback.");
            try
            {
                File.Copy(dbPath, backupPath, overwrite: false);
                _logger.LogInformation("Database safety backup created via fallback file copy at: {BackupPath}", backupPath);
                return backupPath;
            }
            catch (Exception fallbackEx)
            {
                var error = $"Failed to create pre-migration database backup. Migration aborted to prevent data loss. Error: {fallbackEx.Message}";
                _logger.LogCritical(fallbackEx, "{ErrorMessage}", error);
                throw new InvalidOperationException(error, fallbackEx);
            }
        }
    }

    private async Task HandleLegacyEnsureCreatedTransitionAsync(string? backupPath, CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var wasClosed = connection.State == System.Data.ConnectionState.Closed;
        if (wasClosed)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            // Read all existing user tables from sqlite_master
            var existingTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var tableCmd = connection.CreateCommand())
            {
                tableCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";
                using var reader = await tableCmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    existingTables.Add(reader.GetString(0));
                }
            }

            // Fresh database: no tables exist yet. EF Core MigrateAsync will create everything cleanly.
            if (existingTables.Count == 0)
            {
                _logger.LogInformation("Fresh database detected. Standard migrations will apply.");
                return;
            }

            // If __EFMigrationsHistory is present, migrations are already tracked.
            if (existingTables.Contains("__EFMigrationsHistory"))
            {
                _logger.LogInformation("__EFMigrationsHistory detected. Database is already under migration control.");
                return;
            }

            // If tables exist but __EFMigrationsHistory does not, this is a legacy database.
            // Validate the complete schema before applying any modifications.
            _logger.LogInformation("Legacy database without __EFMigrationsHistory detected. Validating schema before migration...");
            await ValidateLegacySchemaAsync(connection, existingTables, backupPath, cancellationToken);

            // Validation passed! Safely bootstrap __EFMigrationsHistory with the initial migration.
            _logger.LogInformation("Legacy database schema validation passed. Bootstrapping __EFMigrationsHistory to preserve existing data...");

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
            _logger.LogInformation("Successfully bootstrapped initial migration into __EFMigrationsHistory. Existing user data preserved.");
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task ValidateLegacySchemaAsync(
        DbConnection connection,
        HashSet<string> existingTables,
        string? backupPath,
        CancellationToken cancellationToken)
    {
        // 1. Check that all expected tables exist
        var missingTables = ExpectedLegacySchema.Keys.Where(t => !existingTables.Contains(t)).ToList();
        if (missingTables.Count > 0)
        {
            var msg = $"Incompatible legacy database schema detected: Missing required tables: [{string.Join(", ", missingTables)}]. " +
                      "Migration aborted to protect user data. The database was not modified.";
            _logger.LogError("{ErrorMessage}", msg);
            throw new IncompatibleDatabaseSchemaException(msg, backupPath);
        }

        // 2. Check each table for expected columns
        foreach (var (tableName, expectedColumns) in ExpectedLegacySchema)
        {
            var actualColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var colCmd = connection.CreateCommand())
            {
                colCmd.CommandText = $"PRAGMA table_info('{tableName}');";
                using var reader = await colCmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    // PRAGMA table_info columns: 0=cid, 1=name, 2=type, 3=notnull, 4=dflt_value, 5=pk
                    actualColumns.Add(reader.GetString(1));
                }
            }

            var missingColumns = expectedColumns.Where(c => !actualColumns.Contains(c)).ToList();
            if (missingColumns.Count > 0)
            {
                var msg = $"Incompatible legacy database schema detected: Table '{tableName}' is missing required columns: [{string.Join(", ", missingColumns)}]. " +
                          "Migration aborted to protect user data. The database was not modified.";
                _logger.LogError("{ErrorMessage}", msg);
                throw new IncompatibleDatabaseSchemaException(msg, backupPath);
            }
        }

        _logger.LogInformation("Legacy database schema validation successful: all expected tables and columns are present.");
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
