using Dragonfly44.Domain.Entities;
using Dragonfly44.Infrastructure.Configuration;
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
}
