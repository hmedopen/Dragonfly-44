using Dragonfly44.Application.Common;
using Dragonfly44.Application.Services;
using Dragonfly44.Infrastructure.Configuration;
using Dragonfly44.Infrastructure.Imaging;
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

public class MangaIngestionTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _tempStorageDir;
    private readonly string _tempSourceDir;
    private readonly Dragonfly44DbContext _dbContext;
    private readonly SqliteDatabaseInitializer _initializer;
    private readonly ProjectRepository _projectRepository;
    private readonly ChapterRepository _chapterRepository;
    private readonly PageRepository _pageRepository;
    private readonly DraftRepository _draftRepository;
    private readonly FileSystemStorageService _storageService;
    private readonly ImageMetadataReader _metadataReader;
    private readonly ProjectService _projectService;
    private readonly ChapterService _chapterService;
    private readonly MangaIngestionService _ingestionService;

    public MangaIngestionTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"dragonfly44_ingest_test_{Guid.NewGuid():N}.db");
        _tempStorageDir = Path.Combine(Path.GetTempPath(), $"dragonfly44_ingest_storage_{Guid.NewGuid():N}");
        _tempSourceDir = Path.Combine(Path.GetTempPath(), $"dragonfly44_ingest_source_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempStorageDir);
        Directory.CreateDirectory(_tempSourceDir);

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
        _metadataReader = new ImageMetadataReader(NullLogger<ImageMetadataReader>.Instance);
        _projectRepository = new ProjectRepository(_dbContext);
        _chapterRepository = new ChapterRepository(_dbContext);
        _pageRepository = new PageRepository(_dbContext);
        _draftRepository = new DraftRepository(_dbContext);

        _projectService = new ProjectService(_projectRepository, NullLogger<ProjectService>.Instance, _storageService);
        _chapterService = new ChapterService(_chapterRepository, _projectRepository, _storageService, NullLogger<ChapterService>.Instance);
        _ingestionService = new MangaIngestionService(
            _dbContext,
            _chapterRepository,
            _pageRepository,
            _draftRepository,
            _storageService,
            _metadataReader,
            NullLogger<MangaIngestionService>.Instance);
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
        try
        {
            if (Directory.Exists(_tempSourceDir)) Directory.Delete(_tempSourceDir, true);
        }
        catch { }
    }

    private void CreateFakePng(string path, int width, int height)
    {
        using var stream = File.Create(path);
        stream.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        stream.Write(new byte[] { 0x00, 0x00, 0x00, 0x0D });
        stream.Write(new byte[] { 0x49, 0x48, 0x44, 0x52 });
        // Width big-endian
        stream.WriteByte((byte)((width >> 24) & 0xFF));
        stream.WriteByte((byte)((width >> 16) & 0xFF));
        stream.WriteByte((byte)((width >> 8) & 0xFF));
        stream.WriteByte((byte)(width & 0xFF));
        // Height big-endian
        stream.WriteByte((byte)((height >> 24) & 0xFF));
        stream.WriteByte((byte)((height >> 16) & 0xFF));
        stream.WriteByte((byte)((height >> 8) & 0xFF));
        stream.WriteByte((byte)(height & 0xFF));
        stream.Write(new byte[] { 0x08, 0x06, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
    }

    [Fact]
    public async Task IngestFolder_ShouldSortNaturally_AndFilterUnsupportedFiles()
    {
        await _initializer.InitializeAsync();

        var project = (await _projectService.CreateProjectAsync("Manga Proj")).Value!;
        var chapter = (await _chapterService.CreateChapterAsync(project.Id, "Chapter 1")).Value!;

        // Create files in unnatural order with mixed supported and unsupported extensions
        CreateFakePng(Path.Combine(_tempSourceDir, "page11.png"), 1000, 1500);
        CreateFakePng(Path.Combine(_tempSourceDir, "page2.png"), 1000, 1500);
        CreateFakePng(Path.Combine(_tempSourceDir, "page10.png"), 1000, 1500);
        CreateFakePng(Path.Combine(_tempSourceDir, "page1.png"), 1000, 1500);
        CreateFakePng(Path.Combine(_tempSourceDir, "page3.png"), 1000, 1500);
        await File.WriteAllTextAsync(Path.Combine(_tempSourceDir, "notes.txt"), "some notes");
        await File.WriteAllTextAsync(Path.Combine(_tempSourceDir, "script.doc"), "script info");

        var progressList = new List<IngestionProgress>();
        var progress = new Progress<IngestionProgress>(p => progressList.Add(p));

        var result = await _ingestionService.IngestChapterFolderAsync(project.Id, chapter.Id, _tempSourceDir, progress);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(5);

        // Check natural ordering
        var pages = result.Value!;
        pages[0].OriginalFileName.Should().Be("page1.png");
        pages[0].PageNumber.Should().Be(1);

        pages[1].OriginalFileName.Should().Be("page2.png");
        pages[1].PageNumber.Should().Be(2);

        pages[2].OriginalFileName.Should().Be("page3.png");
        pages[2].PageNumber.Should().Be(3);

        pages[3].OriginalFileName.Should().Be("page10.png");
        pages[3].PageNumber.Should().Be(4);

        pages[4].OriginalFileName.Should().Be("page11.png");
        pages[4].PageNumber.Should().Be(5);

        // Check metadata
        foreach (var page in pages)
        {
            page.Width.Should().Be(1000);
            page.Height.Should().Be(1500);
            page.FileFormat.ToUpperInvariant().Should().Be("PNG");
            page.FileSizeBytes.Should().BeGreaterThan(0);
            page.ChapterId.Should().Be(chapter.Id);

            // Storage check
            var absolutePath = _storageService.ResolveAbsolutePath(page.ImageRelativePath);
            File.Exists(absolutePath).Should().BeTrue();
        }

        // Original source files must still exist
        File.Exists(Path.Combine(_tempSourceDir, "page1.png")).Should().BeTrue();
        File.Exists(Path.Combine(_tempSourceDir, "notes.txt")).Should().BeTrue();
    }

    [Fact]
    public async Task IngestFolder_WithDuplicateFilenames_ShouldHandleCollisionSafely()
    {
        await _initializer.InitializeAsync();

        var project = (await _projectService.CreateProjectAsync("Collision Proj")).Value!;
        var chapter = (await _chapterService.CreateChapterAsync(project.Id, "Chapter 1")).Value!;

        CreateFakePng(Path.Combine(_tempSourceDir, "page1.png"), 800, 1200);

        // First import
        var firstResult = await _ingestionService.IngestChapterFolderAsync(project.Id, chapter.Id, _tempSourceDir);
        firstResult.IsSuccess.Should().BeTrue();
        firstResult.Value.Should().HaveCount(1);

        // Second import from the same source folder into the same chapter
        var secondResult = await _ingestionService.IngestChapterFolderAsync(project.Id, chapter.Id, _tempSourceDir);
        secondResult.IsSuccess.Should().BeTrue();
        secondResult.Value.Should().HaveCount(1);

        // The second page should get page number 2
        secondResult.Value![0].PageNumber.Should().Be(2);
        secondResult.Value[0].OriginalFileName.Should().Be("page1.png");

        // Stored relative paths must be distinct (no overwrite)
        firstResult.Value![0].ImageRelativePath.Should().NotBe(secondResult.Value[0].ImageRelativePath);

        // Both files must physically exist on disk
        File.Exists(_storageService.ResolveAbsolutePath(firstResult.Value[0].ImageRelativePath)).Should().BeTrue();
        File.Exists(_storageService.ResolveAbsolutePath(secondResult.Value[0].ImageRelativePath)).Should().BeTrue();
    }

    [Fact]
    public async Task IngestFolder_WhenCancelled_ShouldRollbackStoredFilesAndDatabaseRecords()
    {
        await _initializer.InitializeAsync();

        var project = (await _projectService.CreateProjectAsync("Rollback Proj")).Value!;
        var chapter = (await _chapterService.CreateChapterAsync(project.Id, "Chapter 1")).Value!;

        for (int i = 1; i <= 10; i++)
        {
            CreateFakePng(Path.Combine(_tempSourceDir, $"page_{i:D2}.png"), 800, 1200);
        }

        using var cts = new CancellationTokenSource();
        var processedCount = 0;
        var progress = new Progress<IngestionProgress>(p =>
        {
            processedCount++;
            if (processedCount >= 2)
            {
                cts.Cancel();
            }
        });

        // Act
        var result = await _ingestionService.IngestChapterFolderAsync(project.Id, chapter.Id, _tempSourceDir, progress, cts.Token);

        // Assert failure result with cancellation indication
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("cancelled");

        // Assert database has 0 pages for this chapter
        var pagesInDb = await _pageRepository.GetByChapterIdAsync(chapter.Id);
        pagesInDb.Should().BeEmpty();

        // Assert no orphan image files are left in the managed chapter storage directory
        var chapterPagesDir = Path.Combine(_storageService.GetManagedStorageRoot(), "projects", project.Id.ToString("N"), "chapters", chapter.Id.ToString("N"), "pages");
        if (Directory.Exists(chapterPagesDir))
        {
            Directory.GetFiles(chapterPagesDir).Should().BeEmpty();
        }
    }
}
