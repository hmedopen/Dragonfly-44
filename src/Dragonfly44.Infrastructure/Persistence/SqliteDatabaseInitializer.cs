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

    public record ExpectedColumn(string Name, string Type, bool IsNullable, bool IsPrimaryKey);
    public record ExpectedForeignKey(string TargetTable, string FromColumn, string ToColumn, string OnDelete);
    public record ExpectedIndex(string Name, bool IsUnique, IReadOnlyList<string> Columns);
    public record ExpectedTableSchema(
        IReadOnlyList<ExpectedColumn> Columns,
        IReadOnlyList<ExpectedForeignKey> ForeignKeys,
        IReadOnlyList<ExpectedIndex> Indexes);

    private static readonly Dictionary<string, ExpectedTableSchema> ExpectedLegacyFullSchema = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Projects"] = new(
            Columns: new ExpectedColumn[]
            {
                new("Id", "TEXT", false, true),
                new("Name", "TEXT", false, false),
                new("SourceLanguage", "TEXT", false, false),
                new("TargetLanguage", "TEXT", false, false),
                new("Description", "TEXT", true, false),
                new("RootDirectory", "TEXT", true, false),
                new("CreatedAtUtc", "TEXT", false, false),
                new("UpdatedAtUtc", "TEXT", false, false),
            },
            ForeignKeys: Array.Empty<ExpectedForeignKey>(),
            Indexes: new ExpectedIndex[]
            {
                new("IX_Projects_Name", false, new[] { "Name" })
            }
        ),

        ["Chapters"] = new(
            Columns: new ExpectedColumn[]
            {
                new("Id", "TEXT", false, true),
                new("ProjectId", "TEXT", false, false),
                new("ChapterNumber", "INTEGER", false, false),
                new("Title", "TEXT", false, false),
                new("Status", "INTEGER", false, false),
                new("CreatedAtUtc", "TEXT", false, false),
                new("UpdatedAtUtc", "TEXT", false, false),
            },
            ForeignKeys: new ExpectedForeignKey[]
            {
                new("Projects", "ProjectId", "Id", "CASCADE")
            },
            Indexes: new ExpectedIndex[]
            {
                new("IX_Chapters_ProjectId_ChapterNumber", true, new[] { "ProjectId", "ChapterNumber" })
            }
        ),

        ["Characters"] = new(
            Columns: new ExpectedColumn[]
            {
                new("Id", "TEXT", false, true),
                new("ProjectId", "TEXT", false, false),
                new("CanonicalName", "TEXT", false, false),
                new("LocalizedName", "TEXT", false, false),
                new("Description", "TEXT", true, false),
                new("ColorHex", "TEXT", true, false),
                new("Notes", "TEXT", true, false),
                new("CreatedAtUtc", "TEXT", false, false),
            },
            ForeignKeys: new ExpectedForeignKey[]
            {
                new("Projects", "ProjectId", "Id", "CASCADE")
            },
            Indexes: new ExpectedIndex[]
            {
                new("IX_Characters_ProjectId_CanonicalName", false, new[] { "ProjectId", "CanonicalName" })
            }
        ),

        ["GlossaryEntries"] = new(
            Columns: new ExpectedColumn[]
            {
                new("Id", "TEXT", false, true),
                new("ProjectId", "TEXT", false, false),
                new("SourceTerm", "TEXT", false, false),
                new("TargetTerm", "TEXT", false, false),
                new("Category", "TEXT", false, false),
                new("ContextNotes", "TEXT", true, false),
                new("CreatedAtUtc", "TEXT", false, false),
            },
            ForeignKeys: new ExpectedForeignKey[]
            {
                new("Projects", "ProjectId", "Id", "CASCADE")
            },
            Indexes: new ExpectedIndex[]
            {
                new("IX_GlossaryEntries_ProjectId_SourceTerm", false, new[] { "ProjectId", "SourceTerm" })
            }
        ),

        ["ChapterContexts"] = new(
            Columns: new ExpectedColumn[]
            {
                new("Id", "TEXT", false, true),
                new("ChapterId", "TEXT", false, false),
                new("Summary", "TEXT", false, false),
                new("ToneGuidelines", "TEXT", false, false),
                new("CulturalNotes", "TEXT", false, false),
                new("ConsistencyRules", "TEXT", false, false),
                new("UpdatedAtUtc", "TEXT", false, false),
            },
            ForeignKeys: new ExpectedForeignKey[]
            {
                new("Chapters", "ChapterId", "Id", "CASCADE")
            },
            Indexes: new ExpectedIndex[]
            {
                new("IX_ChapterContexts_ChapterId", true, new[] { "ChapterId" })
            }
        ),

        ["Pages"] = new(
            Columns: new ExpectedColumn[]
            {
                new("Id", "TEXT", false, true),
                new("ChapterId", "TEXT", false, false),
                new("PageNumber", "INTEGER", false, false),
                new("ImageRelativePath", "TEXT", false, false),
                new("Width", "INTEGER", false, false),
                new("Height", "INTEGER", false, false),
                new("Status", "INTEGER", false, false),
                new("CreatedAtUtc", "TEXT", false, false),
            },
            ForeignKeys: new ExpectedForeignKey[]
            {
                new("Chapters", "ChapterId", "Id", "CASCADE")
            },
            Indexes: new ExpectedIndex[]
            {
                new("IX_Pages_ChapterId_PageNumber", true, new[] { "ChapterId", "PageNumber" })
            }
        ),

        ["Bubbles"] = new(
            Columns: new ExpectedColumn[]
            {
                new("Id", "TEXT", false, true),
                new("PageId", "TEXT", false, false),
                new("CharacterId", "TEXT", true, false),
                new("X", "REAL", false, false),
                new("Y", "REAL", false, false),
                new("Width", "REAL", false, false),
                new("Height", "REAL", false, false),
                new("RawText", "TEXT", false, false),
                new("TranslatedText", "TEXT", false, false),
                new("ReadingOrderIndex", "INTEGER", false, false),
                new("Confidence", "REAL", false, false),
                new("ShapeType", "INTEGER", false, false),
                new("CreatedAtUtc", "TEXT", false, false),
            },
            ForeignKeys: new ExpectedForeignKey[]
            {
                new("Characters", "CharacterId", "Id", "SET NULL"),
                new("Pages", "PageId", "Id", "CASCADE")
            },
            Indexes: new ExpectedIndex[]
            {
                new("IX_Bubbles_CharacterId", false, new[] { "CharacterId" }),
                new("IX_Bubbles_PageId_ReadingOrderIndex", false, new[] { "PageId", "ReadingOrderIndex" })
            }
        )
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

        // Step 1: Check whether a schema modification (legacy transition or pending EF migration) is required.
        // We only create a backup immediately before modifying an existing database schema.
        var isPhysical = IsPhysicalDatabase(dbPath) && File.Exists(dbPath) && new FileInfo(dbPath).Length > 0;
        string? backupPath = null;

        var (isLegacyTransition, hasPendingMigrations) = await CheckMigrationRequirementsAsync(cancellationToken);

        if (isPhysical && (isLegacyTransition || hasPendingMigrations))
        {
            _logger.LogInformation("Database schema modifications are required (LegacyTransition: {Legacy}, PendingMigrations: {Pending}). Creating pre-modification backup...",
                isLegacyTransition, hasPendingMigrations);
            backupPath = await CreateConsistentBackupAsync(dbPath, cancellationToken);
        }
        else
        {
            _logger.LogInformation("No schema modifications required or fresh database. Skipping database backup.");
        }

        // Step 2: Validate existing legacy schema and handle legacy EnsureCreated transition safely
        if (isLegacyTransition)
        {
            await HandleLegacyEnsureCreatedTransitionAsync(backupPath, cancellationToken);
        }

        // Step 3: Apply any pending EF Core migrations
        if (hasPendingMigrations || isLegacyTransition)
        {
            _logger.LogInformation("Applying EF Core migrations...");
            await _dbContext.Database.MigrateAsync(cancellationToken);
        }

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

    private async Task<(bool IsLegacyTransition, bool HasPendingMigrations)> CheckMigrationRequirementsAsync(CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var wasClosed = connection.State == System.Data.ConnectionState.Closed;
        if (wasClosed)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var userTablesCount = 0L;
            var historyTableCount = 0L;

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name != '__EFMigrationsHistory';";
                userTablesCount = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken));
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory';";
                historyTableCount = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken));
            }

            // Legacy transition is needed if user tables exist but __EFMigrationsHistory does not
            var isLegacy = userTablesCount > 0 && historyTableCount == 0;

            var hasPending = false;
            if (historyTableCount > 0)
            {
                var pending = await _dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
                hasPending = pending.Any();
            }
            else if (userTablesCount == 0)
            {
                // Fresh database: initial create migration is pending
                hasPending = true;
            }

            return (isLegacy, hasPending);
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<string> CreateConsistentBackupAsync(string dbPath, CancellationToken cancellationToken)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var shortGuid = Guid.NewGuid().ToString("N")[..8];
        var backupPath = $"{dbPath}.bak_{timestamp}_{shortGuid}";

        _logger.LogInformation("Creating consistent SQLite backup before schema modification at: {BackupPath}", backupPath);

        var connection = _dbContext.Database.GetDbConnection();
        var wasClosed = connection.State == System.Data.ConnectionState.Closed;
        if (wasClosed)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            // Execute atomic SQLite VACUUM INTO to safely checkpoint and snapshot WAL data
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"VACUUM INTO '{backupPath.Replace("'", "''")}';";
            await cmd.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogInformation("Consistent SQLite backup created successfully at: {BackupPath}", backupPath);
            return backupPath;
        }
        catch (Exception ex)
        {
            var error = $"Failed to create consistent SQLite backup via VACUUM INTO: {ex.Message}. Schema modification aborted to preserve original database.";
            _logger.LogCritical(ex, "{ErrorMessage}", error);
            throw new InvalidOperationException(error, ex);
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync();
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

            // Validate the exhaustive schema before applying any modifications.
            _logger.LogInformation("Legacy database without __EFMigrationsHistory detected. Performing deep schema validation against EnsureCreated specification...");
            await ValidateExhaustiveLegacySchemaAsync(connection, existingTables, backupPath, cancellationToken);

            // Validation passed 100%! Safely bootstrap __EFMigrationsHistory with the initial migration.
            _logger.LogInformation("Exhaustive legacy schema validation passed. Bootstrapping __EFMigrationsHistory to preserve existing data...");

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

    private async Task ValidateExhaustiveLegacySchemaAsync(
        DbConnection connection,
        HashSet<string> existingTables,
        string? backupPath,
        CancellationToken cancellationToken)
    {
        // 1. Check all required tables exist
        var missingTables = ExpectedLegacyFullSchema.Keys.Where(t => !existingTables.Contains(t)).ToList();
        if (missingTables.Count > 0)
        {
            var msg = $"Incompatible legacy database schema: Missing required tables: [{string.Join(", ", missingTables)}]. " +
                      "Migration aborted to protect user data. The database was not modified.";
            _logger.LogError("{ErrorMessage}", msg);
            throw new IncompatibleDatabaseSchemaException(msg, backupPath);
        }

        foreach (var (tableName, expected) in ExpectedLegacyFullSchema)
        {
            // 2. Validate columns (name, type, nullability, primary key)
            var actualColumns = new Dictionary<string, (string Type, bool NotNull, bool Pk)>(StringComparer.OrdinalIgnoreCase);
            using (var colCmd = connection.CreateCommand())
            {
                colCmd.CommandText = $"PRAGMA table_info('{tableName}');";
                using var reader = await colCmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    // PRAGMA table_info columns: 0=cid, 1=name, 2=type, 3=notnull, 4=dflt_value, 5=pk
                    var colName = reader.GetString(1);
                    var colType = reader.GetString(2);
                    var notNull = reader.GetInt32(3) == 1;
                    var pk = reader.GetInt32(5) > 0;
                    actualColumns[colName] = (colType, notNull, pk);
                }
            }

            foreach (var expectedCol in expected.Columns)
            {
                if (!actualColumns.TryGetValue(expectedCol.Name, out var actualCol))
                {
                    var msg = $"Incompatible legacy database schema: Table '{tableName}' is missing column '{expectedCol.Name}'. " +
                              "Migration aborted to protect user data. The database was not modified.";
                    _logger.LogError("{ErrorMessage}", msg);
                    throw new IncompatibleDatabaseSchemaException(msg, backupPath);
                }

                if (!string.Equals(expectedCol.Type, actualCol.Type, StringComparison.OrdinalIgnoreCase))
                {
                    var msg = $"Incompatible legacy database schema: Table '{tableName}', column '{expectedCol.Name}' has type '{actualCol.Type}' but expected '{expectedCol.Type}'. " +
                              "Migration aborted to protect user data. The database was not modified.";
                    _logger.LogError("{ErrorMessage}", msg);
                    throw new IncompatibleDatabaseSchemaException(msg, backupPath);
                }

                if (expectedCol.IsNullable == actualCol.NotNull) // if expectedNullable=true, notNull should be false
                {
                    var msg = $"Incompatible legacy database schema: Table '{tableName}', column '{expectedCol.Name}' nullability mismatch (Expected nullable: {expectedCol.IsNullable}, Actual NotNull: {actualCol.NotNull}). " +
                              "Migration aborted to protect user data. The database was not modified.";
                    _logger.LogError("{ErrorMessage}", msg);
                    throw new IncompatibleDatabaseSchemaException(msg, backupPath);
                }

                if (expectedCol.IsPrimaryKey != actualCol.Pk)
                {
                    var msg = $"Incompatible legacy database schema: Table '{tableName}', column '{expectedCol.Name}' primary key mismatch (Expected PK: {expectedCol.IsPrimaryKey}, Actual PK: {actualCol.Pk}). " +
                              "Migration aborted to protect user data. The database was not modified.";
                    _logger.LogError("{ErrorMessage}", msg);
                    throw new IncompatibleDatabaseSchemaException(msg, backupPath);
                }
            }

            // 3. Validate Foreign Keys
            var actualFks = new List<ExpectedForeignKey>();
            using (var fkCmd = connection.CreateCommand())
            {
                fkCmd.CommandText = $"PRAGMA foreign_key_list('{tableName}');";
                using var reader = await fkCmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    // PRAGMA foreign_key_list: 0=id, 1=seq, 2=table, 3=from, 4=to, 5=on_update, 6=on_delete, 7=match
                    var targetTable = reader.GetString(2);
                    var fromCol = reader.GetString(3);
                    var toCol = reader.IsDBNull(4) ? "Id" : reader.GetString(4);
                    var onDelete = reader.GetString(6);
                    actualFks.Add(new ExpectedForeignKey(targetTable, fromCol, toCol, onDelete));
                }
            }

            foreach (var expectedFk in expected.ForeignKeys)
            {
                var match = actualFks.FirstOrDefault(fk =>
                    string.Equals(fk.TargetTable, expectedFk.TargetTable, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(fk.FromColumn, expectedFk.FromColumn, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(fk.ToColumn, expectedFk.ToColumn, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(fk.OnDelete, expectedFk.OnDelete, StringComparison.OrdinalIgnoreCase));

                if (match == null)
                {
                    var msg = $"Incompatible legacy database schema: Table '{tableName}' is missing foreign key to '{expectedFk.TargetTable}'({expectedFk.ToColumn}) from '{expectedFk.FromColumn}' with ON DELETE {expectedFk.OnDelete}. " +
                              "Migration aborted to protect user data. The database was not modified.";
                    _logger.LogError("{ErrorMessage}", msg);
                    throw new IncompatibleDatabaseSchemaException(msg, backupPath);
                }
            }

            // 4. Validate Indexes and Unique Constraints
            var actualIndexes = new List<ExpectedIndex>();
            using (var idxCmd = connection.CreateCommand())
            {
                idxCmd.CommandText = $"PRAGMA index_list('{tableName}');";
                using var reader = await idxCmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    // PRAGMA index_list: 0=seq, 1=name, 2=unique, 3=origin, 4=partial
                    var idxName = reader.GetString(1);
                    var isUnique = reader.GetInt32(2) == 1;

                    // Skip SQLite internal primary key auto-indexes
                    if (idxName.StartsWith("sqlite_autoindex_", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    actualIndexes.Add(new ExpectedIndex(idxName, isUnique, Array.Empty<string>()));
                }
            }

            // Read index column definitions
            for (int i = 0; i < actualIndexes.Count; i++)
            {
                var idx = actualIndexes[i];
                var cols = new List<string>();
                using (var infoCmd = connection.CreateCommand())
                {
                    infoCmd.CommandText = $"PRAGMA index_info('{idx.Name}');";
                    using var reader = await infoCmd.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        // 0=seqno, 1=cid, 2=name
                        cols.Add(reader.GetString(2));
                    }
                }
                actualIndexes[i] = idx with { Columns = cols };
            }

            foreach (var expectedIdx in expected.Indexes)
            {
                var match = actualIndexes.FirstOrDefault(idx =>
                    idx.IsUnique == expectedIdx.IsUnique &&
                    idx.Columns.SequenceEqual(expectedIdx.Columns, StringComparer.OrdinalIgnoreCase));

                if (match == null)
                {
                    var msg = $"Incompatible legacy database schema: Table '{tableName}' is missing index '{expectedIdx.Name}' (Unique: {expectedIdx.IsUnique}, Columns: [{string.Join(", ", expectedIdx.Columns)}]). " +
                              "Migration aborted to protect user data. The database was not modified.";
                    _logger.LogError("{ErrorMessage}", msg);
                    throw new IncompatibleDatabaseSchemaException(msg, backupPath);
                }
            }
        }

        _logger.LogInformation("Exhaustive legacy database schema validation successful: all tables, columns, types, nullability, PKs, FKs, and indexes match the EnsureCreated specification.");
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
