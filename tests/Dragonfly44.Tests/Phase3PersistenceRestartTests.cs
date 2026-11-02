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

public class Phase3PersistenceRestartTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _tempStorageDir;
    private readonly string _tempSourceDir;

    public Phase3PersistenceRestartTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"dragonfly44_restart_test_{Guid.NewGuid():N}.db");
        _tempStorageDir = Path.Combine(Path.GetTempPath(), $"dragonfly44_restart_storage_{Guid.NewGuid():N}");
        _tempSourceDir = Path.Combine(Path.GetTempPath(), $"dragonfly44_restart_source_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempStorageDir);
        Directory.CreateDirectory(_tempSourceDir);
    }

    public void Dispose()
    {
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
        stream.WriteByte((byte)((width >> 24) & 0xFF));
        stream.WriteByte((byte)((width >> 16) & 0xFF));
        stream.WriteByte((byte)((width >> 8) & 0xFF));
        stream.WriteByte((byte)(width & 0xFF));
        stream.WriteByte((byte)((height >> 24) & 0xFF));
        stream.WriteByte((byte)((height >> 16) & 0xFF));
        stream.WriteByte((byte)((height >> 8) & 0xFF));
        stream.WriteByte((byte)(height & 0xFF));
        stream.Write(new byte[] { 0x08, 0x06, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
    }

    [Fact]
    public async Task AppRestart_ShouldPersistProjectsChaptersPagesAndDrafts()
    {
        Guid projectId;
        Guid chapterId;
        Guid draftId;

        var storageOptions = Options.Create(new StorageOptions { AppDataDirectory = _tempStorageDir });
        var dbOptions = Options.Create(new DatabaseOptions
        {
            ConnectionString = $"Data Source={_testDbPath}",
            DatabaseFileName = Path.GetFileName(_testDbPath)
        });

        // ------------------ SESSION 1: Create and Ingest ------------------
        {
            var options = new DbContextOptionsBuilder<Dragonfly44DbContext>()
                .UseSqlite($"Data Source={_testDbPath}")
                .Options;

            using var dbContext = new Dragonfly44DbContext(options);
            var initializer = new SqliteDatabaseInitializer(dbContext, dbOptions, storageOptions, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync();

            var storage = new FileSystemStorageService(storageOptions, NullLogger<FileSystemStorageService>.Instance);
            var metaReader = new ImageMetadataReader(NullLogger<ImageMetadataReader>.Instance);
            var projRepo = new ProjectRepository(dbContext);
            var chapRepo = new ChapterRepository(dbContext);
            var pageRepo = new PageRepository(dbContext);
            var draftRepo = new DraftRepository(dbContext);

            var projService = new ProjectService(projRepo, NullLogger<ProjectService>.Instance, storage);
            var chapService = new ChapterService(chapRepo, projRepo, storage, NullLogger<ChapterService>.Instance);
            var draftService = new DraftService(draftRepo, storage, NullLogger<DraftService>.Instance);
            var ingestService = new MangaIngestionService(dbContext, chapRepo, pageRepo, draftRepo, storage, metaReader, NullLogger<MangaIngestionService>.Instance);

            var proj = (await projService.CreateProjectAsync("Vagabond", "ja", "ar", "Takehiko Inoue masterwork")).Value!;
            projectId = proj.Id;

            var chap = (await chapService.CreateChapterAsync(projectId, "Chapter 1: Shinmen Takezo")).Value!;
            chapterId = chap.Id;

            CreateFakePng(Path.Combine(_tempSourceDir, "p1.png"), 1200, 1800);
            CreateFakePng(Path.Combine(_tempSourceDir, "p2.png"), 1200, 1800);
            await ingestService.IngestChapterFolderAsync(projectId, chapterId, _tempSourceDir);

            var draft = (await draftService.CreateDraftAsync("Cover Ideas", "Rough sketches")).Value!;
            draftId = draft.Id;
            await ingestService.IngestDraftFolderAsync(draftId, _tempSourceDir);
        }

        // ------------------ SESSION 2: Simulate Restart with New Context ------------------
        {
            var options = new DbContextOptionsBuilder<Dragonfly44DbContext>()
                .UseSqlite($"Data Source={_testDbPath}")
                .Options;

            using var dbContext = new Dragonfly44DbContext(options);
            var initializer = new SqliteDatabaseInitializer(dbContext, dbOptions, storageOptions, NullLogger<SqliteDatabaseInitializer>.Instance);
            await initializer.InitializeAsync(); // Should detect existing schema cleanly

            var storage = new FileSystemStorageService(storageOptions, NullLogger<FileSystemStorageService>.Instance);
            var projRepo = new ProjectRepository(dbContext);
            var chapRepo = new ChapterRepository(dbContext);
            var pageRepo = new PageRepository(dbContext);
            var draftRepo = new DraftRepository(dbContext);

            // Verify Project
            var reloadedProject = await projRepo.GetByIdAsync(projectId);
            reloadedProject.Should().NotBeNull();
            reloadedProject!.Name.Should().Be("Vagabond");
            reloadedProject.Description.Should().Be("Takehiko Inoue masterwork");

            // Verify Chapter
            var reloadedChapters = await chapRepo.GetByProjectIdAsync(projectId);
            reloadedChapters.Should().HaveCount(1);
            var reloadedChapter = reloadedChapters[0];
            reloadedChapter.Id.Should().Be(chapterId);
            reloadedChapter.Title.Should().Be("Chapter 1: Shinmen Takezo");

            // Verify Pages & Disk Files
            var reloadedPages = await pageRepo.GetByChapterIdAsync(chapterId);
            reloadedPages.Should().HaveCount(2);
            reloadedPages[0].OriginalFileName.Should().Be("p1.png");
            reloadedPages[0].PageNumber.Should().Be(1);
            reloadedPages[0].Width.Should().Be(1200);
            reloadedPages[0].Height.Should().Be(1800);
            File.Exists(storage.ResolveAbsolutePath(reloadedPages[0].ImageRelativePath)).Should().BeTrue();

            reloadedPages[1].OriginalFileName.Should().Be("p2.png");
            reloadedPages[1].PageNumber.Should().Be(2);
            File.Exists(storage.ResolveAbsolutePath(reloadedPages[1].ImageRelativePath)).Should().BeTrue();

            // Verify Draft & Draft Pages
            var reloadedDraft = await draftRepo.GetByIdAsync(draftId);
            reloadedDraft.Should().NotBeNull();
            reloadedDraft!.Name.Should().Be("Cover Ideas");
            reloadedDraft.Pages.Should().HaveCount(2);
            File.Exists(storage.ResolveAbsolutePath(reloadedDraft.Pages[0].ImageRelativePath)).Should().BeTrue();
            File.Exists(storage.ResolveAbsolutePath(reloadedDraft.Pages[1].ImageRelativePath)).Should().BeTrue();
        }
    }
}
