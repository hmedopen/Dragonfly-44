using Dragonfly44.Application.Services;
using Dragonfly44.Infrastructure.Configuration;
using Dragonfly44.Infrastructure.Persistence;
using Dragonfly44.Infrastructure.Persistence.Repositories;
using Dragonfly44.Infrastructure.Services;
using Dragonfly44.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dragonfly44.Tests;

public class ChapterManagementTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _tempStorageDir;
    private readonly Dragonfly44DbContext _dbContext;
    private readonly SqliteDatabaseInitializer _initializer;
    private readonly ProjectRepository _projectRepository;
    private readonly ChapterRepository _chapterRepository;
    private readonly FileSystemStorageService _storageService;
    private readonly ProjectService _projectService;
    private readonly ChapterService _chapterService;

    public ChapterManagementTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"dragonfly44_chap_test_{Guid.NewGuid():N}.db");
        _tempStorageDir = Path.Combine(Path.GetTempPath(), $"dragonfly44_chap_storage_{Guid.NewGuid():N}");
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
        _chapterRepository = new ChapterRepository(_dbContext);
        _projectService = new ProjectService(_projectRepository, NullLogger<ProjectService>.Instance, _storageService);
        _chapterService = new ChapterService(_chapterRepository, _projectRepository, _storageService, NullLogger<ChapterService>.Instance);
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
    public async Task CreateChapter_ShouldAssignCorrectChapterNumberAndBelongToProject()
    {
        await _initializer.InitializeAsync();

        var project = (await _projectService.CreateProjectAsync("One Piece")).Value!;

        var ch1 = await _chapterService.CreateChapterAsync(project.Id, "Romance Dawn");
        var ch2 = await _chapterService.CreateChapterAsync(project.Id, "They Call Him Straw Hat Luffy");
        var ch3 = await _chapterService.CreateChapterAsync(project.Id, "Enter Zoro");

        ch1.IsSuccess.Should().BeTrue();
        ch1.Value!.ChapterNumber.Should().Be(1);
        ch1.Value.ProjectId.Should().Be(project.Id);

        ch2.IsSuccess.Should().BeTrue();
        ch2.Value!.ChapterNumber.Should().Be(2);

        ch3.IsSuccess.Should().BeTrue();
        ch3.Value!.ChapterNumber.Should().Be(3);

        var chapters = await _chapterService.GetChaptersByProjectAsync(project.Id);
        chapters.Should().HaveCount(3);
        chapters.Select(c => c.Title).Should().ContainInOrder(
            "Romance Dawn",
            "They Call Him Straw Hat Luffy",
            "Enter Zoro"
        );
    }

    [Fact]
    public async Task RenameChapter_ShouldUpdateTitle()
    {
        await _initializer.InitializeAsync();

        var project = (await _projectService.CreateProjectAsync("Naruto")).Value!;
        var chapter = (await _chapterService.CreateChapterAsync(project.Id, "Uzumaki Naruto!")).Value!;

        var renamed = await _chapterService.RenameChapterAsync(chapter.Id, "Uzumaki Naruto (Revised)");

        renamed.IsSuccess.Should().BeTrue();
        renamed.Value!.Title.Should().Be("Uzumaki Naruto (Revised)");

        var retrieved = await _chapterService.GetChapterAsync(chapter.Id);
        retrieved!.Title.Should().Be("Uzumaki Naruto (Revised)");
    }

    [Fact]
    public async Task DeleteChapter_ShouldRemoveChapter_AndCleanStorageDirectory()
    {
        await _initializer.InitializeAsync();

        var project = (await _projectService.CreateProjectAsync("Bleach")).Value!;
        var chapter = (await _chapterService.CreateChapterAsync(project.Id, "Death & Strawberry")).Value!;

        // Add dummy storage file
        var chapterStorageDir = Path.Combine(_storageService.GetManagedStorageRoot(), "projects", project.Id.ToString("N"), "chapters", chapter.Id.ToString("N"));
        Directory.CreateDirectory(chapterStorageDir);
        var dummyFile = Path.Combine(chapterStorageDir, "page1.png");
        await File.WriteAllTextAsync(dummyFile, "dummy");

        File.Exists(dummyFile).Should().BeTrue();

        var deleteResult = await _chapterService.DeleteChapterAsync(chapter.Id);
        deleteResult.IsSuccess.Should().BeTrue();

        var retrieved = await _chapterService.GetChapterAsync(chapter.Id);
        retrieved.Should().BeNull();

        Directory.Exists(chapterStorageDir).Should().BeFalse();
    }

    [Fact]
    public async Task ChapterIsolation_ShouldOnlyReturnChaptersForSpecificProject()
    {
        await _initializer.InitializeAsync();

        var projA = (await _projectService.CreateProjectAsync("Project A")).Value!;
        var projB = (await _projectService.CreateProjectAsync("Project B")).Value!;

        await _chapterService.CreateChapterAsync(projA.Id, "Proj A Ch 1");
        await _chapterService.CreateChapterAsync(projA.Id, "Proj A Ch 2");
        await _chapterService.CreateChapterAsync(projB.Id, "Proj B Ch 1");

        var chaptersA = await _chapterService.GetChaptersByProjectAsync(projA.Id);
        var chaptersB = await _chapterService.GetChaptersByProjectAsync(projB.Id);

        chaptersA.Should().HaveCount(2);
        chaptersA.All(c => c.ProjectId == projA.Id).Should().BeTrue();

        chaptersB.Should().HaveCount(1);
        chaptersB.All(c => c.ProjectId == projB.Id).Should().BeTrue();
    }
}
