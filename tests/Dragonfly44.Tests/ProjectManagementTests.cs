using Dragonfly44.Application.Services;
using Dragonfly44.Infrastructure.Configuration;
using Dragonfly44.Infrastructure.Persistence;
using Dragonfly44.Infrastructure.Persistence.Repositories;
using Dragonfly44.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dragonfly44.Tests;

public class ProjectManagementTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _tempStorageDir;
    private readonly Dragonfly44DbContext _dbContext;
    private readonly SqliteDatabaseInitializer _initializer;
    private readonly ProjectRepository _projectRepository;
    private readonly FileSystemStorageService _storageService;
    private readonly ProjectService _projectService;

    public ProjectManagementTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"dragonfly44_proj_test_{Guid.NewGuid():N}.db");
        _tempStorageDir = Path.Combine(Path.GetTempPath(), $"dragonfly44_proj_storage_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempStorageDir);

        var options = new DbContextOptionsBuilder<Dragonfly44DbContext>()
            .UseSqlite($"Data Source={_testDbPath}")
            .Options;

        _dbContext = new Dragonfly44DbContext(options);

        var dbOptions = Options.Create(new DatabaseOptions
        {
            ConnectionString = $"Data Source={_testDbPath}",
            DatabaseFileName = Path.GetFileName(_testDbPath)
        });

        var storageOptions = Options.Create(new StorageOptions
        {
            AppDataDirectory = _tempStorageDir
        });

        _initializer = new SqliteDatabaseInitializer(
            _dbContext,
            dbOptions,
            storageOptions,
            NullLogger<SqliteDatabaseInitializer>.Instance);

        _storageService = new FileSystemStorageService(storageOptions, NullLogger<FileSystemStorageService>.Instance);
        _projectRepository = new ProjectRepository(_dbContext);
        _projectService = new ProjectService(_projectRepository, NullLogger<ProjectService>.Instance, _storageService);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            if (File.Exists(_testDbPath)) File.Delete(_testDbPath);
        }
        catch { }
        try
        {
            if (Directory.Exists(_tempStorageDir)) Directory.Delete(_tempStorageDir, true);
        }
        catch { }
    }

    [Fact]
    public async Task CreateProject_ShouldPersistProject_AndInitializeTimestamps()
    {
        await _initializer.InitializeAsync();

        var result = await _projectService.CreateProjectAsync("Berserk", "ja", "ar", "A dark fantasy manga");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("Berserk");
        result.Value.Description.Should().Be("A dark fantasy manga");
        result.Value.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.Value.UpdatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        var retrieved = await _projectService.GetProjectAsync(result.Value.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Berserk");
    }

    [Fact]
    public async Task CreateProject_WithEmptyName_ShouldFail()
    {
        await _initializer.InitializeAsync();

        var result = await _projectService.CreateProjectAsync("   ");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task RenameProject_ShouldUpdateNameAndTimestamp()
    {
        await _initializer.InitializeAsync();

        var created = await _projectService.CreateProjectAsync("Original Title");
        var originalUpdated = created.Value!.UpdatedAtUtc;

        await Task.Delay(50);

        var renamed = await _projectService.RenameProjectAsync(created.Value.Id, "Renamed Title");

        renamed.IsSuccess.Should().BeTrue();
        renamed.Value!.Name.Should().Be("Renamed Title");
        renamed.Value.UpdatedAtUtc.Should().BeAfter(originalUpdated);

        var retrieved = await _projectService.GetProjectAsync(created.Value.Id);
        retrieved!.Name.Should().Be("Renamed Title");
    }

    [Fact]
    public async Task DeleteProject_ShouldRemoveProject_AndCleanupStorage()
    {
        await _initializer.InitializeAsync();

        var created = await _projectService.CreateProjectAsync("Project to Delete");
        var projId = created.Value!.Id;

        // Create a dummy file in the project's managed directory
        var dummyChapterId = Guid.NewGuid();
        var dummyFile = Path.Combine(_tempStorageDir, "storage", "projects", projId.ToString("N"), dummyChapterId.ToString("N"), "dummy.png");
        Directory.CreateDirectory(Path.GetDirectoryName(dummyFile)!);
        await File.WriteAllTextAsync(dummyFile, "dummy");

        File.Exists(dummyFile).Should().BeTrue();

        var deleteResult = await _projectService.DeleteProjectAsync(projId);

        deleteResult.IsSuccess.Should().BeTrue();

        var retrieved = await _projectService.GetProjectAsync(projId);
        retrieved.Should().BeNull();

        // Project directory should be deleted from storage
        var projDir = Path.Combine(_tempStorageDir, "storage", "projects", projId.ToString("N"));
        Directory.Exists(projDir).Should().BeFalse();
    }

    [Fact]
    public async Task GetAllProjects_ShouldReturnAllCreatedProjects()
    {
        await _initializer.InitializeAsync();

        await _projectService.CreateProjectAsync("Project A");
        await _projectService.CreateProjectAsync("Project B");
        await _projectService.CreateProjectAsync("Project C");

        var all = await _projectService.GetAllProjectsAsync();
        all.Should().HaveCount(3);
        all.Select(p => p.Name).Should().Contain(new[] { "Project A", "Project B", "Project C" });
    }
}
