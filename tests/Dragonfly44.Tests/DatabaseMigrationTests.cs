using Dragonfly44.Domain.Entities;
using Dragonfly44.Infrastructure.Configuration;
using Dragonfly44.Infrastructure.Exceptions;
using Dragonfly44.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dragonfly44.Tests;

public class DatabaseMigrationTests : IDisposable
{
    private readonly List<string> _tempDbFiles = new();

    private string CreateTempDbPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dragonfly44_migration_{Guid.NewGuid():N}.db");
        _tempDbFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var file in _tempDbFiles)
        {
            try
            {
                if (File.Exists(file)) File.Delete(file);
                var wal = file + "-wal";
                if (File.Exists(wal)) File.Delete(wal);
                var shm = file + "-shm";
                if (File.Exists(shm)) File.Delete(shm);

                // Clean up any test backups created
                var dir = Path.GetDirectoryName(file);
                if (dir != null && Directory.Exists(dir))
                {
                    var baseName = Path.GetFileName(file);
                    foreach (var bak in Directory.GetFiles(dir, $"{baseName}.bak_*"))
                    {
                        try { File.Delete(bak); } catch { }
                    }
                }
            }
            catch
            {
                // Ignore cleanup lock issues on temp files
            }
        }
    }

    [Fact]
    public async Task FreshDatabase_InitializeAsync_ShouldApplyMigrationsAndCreateHistory()
    {
        var dbPath = CreateTempDbPath();
        var options = new DbContextOptionsBuilder<Dragonfly44DbContext>()
            .UseSqlite($"Data Source={dbPath}", b => b.MigrationsAssembly(typeof(Dragonfly44DbContext).Assembly.FullName))
            .Options;

        using var dbContext = new Dragonfly44DbContext(options);
        var initializer = new SqliteDatabaseInitializer(
            dbContext,
            Options.Create(new DatabaseOptions { ConnectionString = $"Data Source={dbPath}" }),
            Options.Create(new StorageOptions { AppDataDirectory = Path.GetDirectoryName(dbPath)! }),
            NullLogger<SqliteDatabaseInitializer>.Instance);

        // Act
        await initializer.InitializeAsync();

        // Assert
        var canConnect = await initializer.CanConnectAsync();
        canConnect.Should().BeTrue();

        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory';";
            var historyCount = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            historyCount.Should().Be(1, "__EFMigrationsHistory must be created on fresh database");

            using var tableCmd = connection.CreateCommand();
            tableCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Projects';";
            var projectsCount = Convert.ToInt64(await tableCmd.ExecuteScalarAsync());
            projectsCount.Should().Be(1, "Projects table must be created by migration");
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    [Fact]
    public async Task ExistingEnsureCreatedDatabase_InitializeAsync_ShouldBootstrapHistoryAndPreserveData()
    {
        var dbPath = CreateTempDbPath();
        var options = new DbContextOptionsBuilder<Dragonfly44DbContext>()
            .UseSqlite($"Data Source={dbPath}", b => b.MigrationsAssembly(typeof(Dragonfly44DbContext).Assembly.FullName))
            .Options;

        var sampleProjectId = Guid.NewGuid();

        // 1. Simulate legacy database created with EnsureCreatedAsync
        using (var legacyContext = new Dragonfly44DbContext(options))
        {
            await legacyContext.Database.EnsureCreatedAsync();

            // Insert sample user data
            legacyContext.Projects.Add(new Project
            {
                Id = sampleProjectId,
                Name = "Legacy User Project",
                SourceLanguage = "ja",
                TargetLanguage = "ar",
                Description = "Pre-existing project created prior to EF migrations"
            });

            await legacyContext.SaveChangesAsync();

            // Confirm __EFMigrationsHistory does NOT exist in EnsureCreated database
            var conn = legacyContext.Database.GetDbConnection();
            await conn.OpenAsync();
            using var chk = conn.CreateCommand();
            chk.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory';";
            var histExists = Convert.ToInt64(await chk.ExecuteScalarAsync());
            await conn.CloseAsync();
            histExists.Should().Be(0, "EnsureCreated databases do not have __EFMigrationsHistory");
        }

        // 2. Initialize with new migration-aware initializer
        using (var migrationContext = new Dragonfly44DbContext(options))
        {
            var initializer = new SqliteDatabaseInitializer(
                migrationContext,
                Options.Create(new DatabaseOptions { ConnectionString = $"Data Source={dbPath}" }),
                Options.Create(new StorageOptions { AppDataDirectory = Path.GetDirectoryName(dbPath)! }),
                NullLogger<SqliteDatabaseInitializer>.Instance);

            // Act - should NOT throw "table Projects already exists"
            var act = async () => await initializer.InitializeAsync();
            await act.Should().NotThrowAsync();

            // Assert: verify pre-existing data was preserved
            var preservedProject = await migrationContext.Projects.FirstOrDefaultAsync(p => p.Id == sampleProjectId);
            preservedProject.Should().NotBeNull("Pre-existing project data must be preserved without loss");
            preservedProject!.Name.Should().Be("Legacy User Project");

            // Verify __EFMigrationsHistory exists
            var conn = migrationContext.Database.GetDbConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM \"__EFMigrationsHistory\";";
            var recordCount = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            await conn.CloseAsync();
            recordCount.Should().BeGreaterThanOrEqualTo(1);
        }
    }

    [Fact]
    public async Task ExistingDatabase_ShouldCreateBackup_BeforeSchemaModification()
    {
        var dbPath = CreateTempDbPath();
        var options = new DbContextOptionsBuilder<Dragonfly44DbContext>()
            .UseSqlite($"Data Source={dbPath}", b => b.MigrationsAssembly(typeof(Dragonfly44DbContext).Assembly.FullName))
            .Options;

        // 1. Create initial database with user data
        using (var legacyContext = new Dragonfly44DbContext(options))
        {
            await legacyContext.Database.EnsureCreatedAsync();
            legacyContext.Projects.Add(new Project
            {
                Id = Guid.NewGuid(),
                Name = "Backup Test Project",
                SourceLanguage = "ja",
                TargetLanguage = "ar"
            });
            await legacyContext.SaveChangesAsync();
        }

        // 2. Run initializer
        using (var context = new Dragonfly44DbContext(options))
        {
            var initializer = new SqliteDatabaseInitializer(
                context,
                Options.Create(new DatabaseOptions { ConnectionString = $"Data Source={dbPath}" }),
                Options.Create(new StorageOptions { AppDataDirectory = Path.GetDirectoryName(dbPath)! }),
                NullLogger<SqliteDatabaseInitializer>.Instance);

            await initializer.InitializeAsync();
        }

        // 3. Assert backup file exists in the directory
        var dir = Path.GetDirectoryName(dbPath)!;
        var baseName = Path.GetFileName(dbPath);
        var backups = Directory.GetFiles(dir, $"{baseName}.bak_*");

        backups.Should().NotBeEmpty("A pre-migration safety backup must be created before modifying an existing database");
        new FileInfo(backups[0]).Length.Should().BeGreaterThan(0, "The backup file must not be empty");
    }

    [Fact]
    public async Task LegacyDatabase_WithMissingTable_ShouldThrowIncompatibleDatabaseSchemaException_AndNeverModifyDatabase()
    {
        var dbPath = CreateTempDbPath();
        var options = new DbContextOptionsBuilder<Dragonfly44DbContext>()
            .UseSqlite($"Data Source={dbPath}", b => b.MigrationsAssembly(typeof(Dragonfly44DbContext).Assembly.FullName))
            .Options;

        var sampleProjectId = Guid.NewGuid();

        // 1. Create an incomplete legacy database (missing Bubbles table)
        using (var rawContext = new Dragonfly44DbContext(options))
        {
            var conn = rawContext.Database.GetDbConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE ""Projects"" (
                    ""Id"" TEXT NOT NULL PRIMARY KEY,
                    ""Name"" TEXT NOT NULL,
                    ""SourceLanguage"" TEXT NOT NULL,
                    ""TargetLanguage"" TEXT NOT NULL,
                    ""Description"" TEXT NULL,
                    ""RootDirectory"" TEXT NULL,
                    ""CreatedAtUtc"" TEXT NOT NULL,
                    ""UpdatedAtUtc"" TEXT NOT NULL
                );
                INSERT INTO ""Projects"" (""Id"", ""Name"", ""SourceLanguage"", ""TargetLanguage"", ""CreatedAtUtc"", ""UpdatedAtUtc"")
                VALUES ('" + sampleProjectId + @"', 'Valuable User Project', 'ja', 'ar', '2026-01-01', '2026-01-01');
            ";
            await cmd.ExecuteNonQueryAsync();
            await conn.CloseAsync();
        }

        // 2. Try to initialize
        using (var migrationContext = new Dragonfly44DbContext(options))
        {
            var initializer = new SqliteDatabaseInitializer(
                migrationContext,
                Options.Create(new DatabaseOptions { ConnectionString = $"Data Source={dbPath}" }),
                Options.Create(new StorageOptions { AppDataDirectory = Path.GetDirectoryName(dbPath)! }),
                NullLogger<SqliteDatabaseInitializer>.Instance);

            // Act & Assert
            var act = async () => await initializer.InitializeAsync();
            var ex = await act.Should().ThrowAsync<IncompatibleDatabaseSchemaException>();
            ex.Which.Message.Should().Contain("Missing required tables");
            ex.Which.Message.Should().Contain("Migration aborted to protect user data");

            // Verify the existing user data was NOT deleted, altered, or overwritten
            var conn = migrationContext.Database.GetDbConnection();
            await conn.OpenAsync();
            using var verifyCmd = conn.CreateCommand();
            verifyCmd.CommandText = "SELECT Name FROM \"Projects\" WHERE \"Id\" = '" + sampleProjectId + "';";
            var projectName = (string?)await verifyCmd.ExecuteScalarAsync();
            await conn.CloseAsync();

            projectName.Should().Be("Valuable User Project", "User data must be preserved intact when migration is rejected");
        }
    }

    [Fact]
    public async Task LegacyDatabase_WithMissingColumn_ShouldThrowIncompatibleDatabaseSchemaException_AndNeverModifyDatabase()
    {
        var dbPath = CreateTempDbPath();
        var options = new DbContextOptionsBuilder<Dragonfly44DbContext>()
            .UseSqlite($"Data Source={dbPath}", b => b.MigrationsAssembly(typeof(Dragonfly44DbContext).Assembly.FullName))
            .Options;

        // 1. Create all 7 tables, but omit RootDirectory from Projects
        using (var rawContext = new Dragonfly44DbContext(options))
        {
            await rawContext.Database.EnsureCreatedAsync();

            // Drop RootDirectory column by recreating Projects table without it
            var conn = rawContext.Database.GetDbConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                PRAGMA foreign_keys = OFF;
                CREATE TABLE ""Projects_Old"" (
                    ""Id"" TEXT NOT NULL PRIMARY KEY,
                    ""Name"" TEXT NOT NULL,
                    ""SourceLanguage"" TEXT NOT NULL,
                    ""TargetLanguage"" TEXT NOT NULL,
                    ""Description"" TEXT NULL,
                    ""CreatedAtUtc"" TEXT NOT NULL,
                    ""UpdatedAtUtc"" TEXT NOT NULL
                );
                DROP TABLE ""Projects"";
                ALTER TABLE ""Projects_Old"" RENAME TO ""Projects"";
                PRAGMA foreign_keys = ON;
            ";
            await cmd.ExecuteNonQueryAsync();
            await conn.CloseAsync();
        }

        // 2. Try to initialize
        using (var migrationContext = new Dragonfly44DbContext(options))
        {
            var initializer = new SqliteDatabaseInitializer(
                migrationContext,
                Options.Create(new DatabaseOptions { ConnectionString = $"Data Source={dbPath}" }),
                Options.Create(new StorageOptions { AppDataDirectory = Path.GetDirectoryName(dbPath)! }),
                NullLogger<SqliteDatabaseInitializer>.Instance);

            // Act & Assert
            var act = async () => await initializer.InitializeAsync();
            var ex = await act.Should().ThrowAsync<IncompatibleDatabaseSchemaException>();
            ex.Which.Message.Should().Contain("Table 'Projects' is missing required columns");
            ex.Which.Message.Should().Contain("RootDirectory");
        }
    }
}
