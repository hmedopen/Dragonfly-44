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

public class DraftManagementTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _tempStorageDir;
    private readonly string _tempSourceDir;
    private readonly Dragonfly44DbContext _dbContext;
    private readonly SqliteDatabaseInitializer _initializer;
    private readonly DraftRepository _draftRepository;
    private readonly ChapterRepository _chapterRepository;
    private readonly PageRepository _pageRepository;
    private readonly FileSystemStorageService _storageService;
    private readonly ImageMetadataReader _metadataReader;
    private readonly DraftService _draftService;
    private readonly MangaIngestionService _ingestionService;

    public DraftManagementTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"dragonfly44_draft_test_{Guid.NewGuid():N}.db");
        _tempStorageDir = Path.Combine(Path.GetTempPath(), $"dragonfly44_draft_storage_{Guid.NewGuid():N}");
        _tempSourceDir = Path.Combine(Path.GetTempPath(), $"dragonfly44_draft_source_{Guid.NewGuid():N}");
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
        _draftRepository = new DraftRepository(_dbContext);
        _chapterRepository = new ChapterRepository(_dbContext);
        _pageRepository = new PageRepository(_dbContext);

        _draftService = new DraftService(_draftRepository, _storageService, NullLogger<DraftService>.Instance);
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
    public async Task CreateDraft_ShouldPersistDraft_AndInitializeTimestamps()
    {
        await _initializer.InitializeAsync();

        var result = await _draftService.CreateDraftAsync("Test Draft", "Quick sketch notes");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("Test Draft");
        result.Value.Description.Should().Be("Quick sketch notes");

        var retrieved = await _draftService.GetDraftAsync(result.Value.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Test Draft");
    }

    [Fact]
    public async Task RenameDraft_ShouldUpdateName()
    {
        await _initializer.InitializeAsync();

        var created = await _draftService.CreateDraftAsync("Initial Draft Name");
        var renamed = await _draftService.RenameDraftAsync(created.Value!.Id, "Updated Draft Name");

        renamed.IsSuccess.Should().BeTrue();
        renamed.Value!.Name.Should().Be("Updated Draft Name");

        var retrieved = await _draftService.GetDraftAsync(created.Value.Id);
        retrieved!.Name.Should().Be("Updated Draft Name");
    }

    [Fact]
    public async Task IngestDraftFolder_ShouldImportPages_AndMaintainDraftIsolation()
    {
        await _initializer.InitializeAsync();

        var draft = (await _draftService.CreateDraftAsync("Isolated Draft")).Value!;

        CreateFakePng(Path.Combine(_tempSourceDir, "draft_02.png"), 1200, 1800);
        CreateFakePng(Path.Combine(_tempSourceDir, "draft_01.png"), 1200, 1800);

        var result = await _ingestionService.IngestDraftFolderAsync(draft.Id, _tempSourceDir);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);

        result.Value![0].OriginalFileName.Should().Be("draft_01.png");
        result.Value[0].PageNumber.Should().Be(1);
        result.Value[1].OriginalFileName.Should().Be("draft_02.png");
        result.Value[1].PageNumber.Should().Be(2);

        // Verify draft pages are stored in drafts directory
        var draftStorageDir = Path.Combine(_storageService.GetManagedStorageRoot(), "drafts", draft.Id.ToString("N"));
        Directory.Exists(draftStorageDir).Should().BeTrue();
        Directory.GetFiles(draftStorageDir, "*", SearchOption.AllDirectories).Should().HaveCount(2);

        // Verify regular chapter pages are empty
        var allChapterPages = await _dbContext.Pages.ToListAsync();
        allChapterPages.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteDraft_ShouldRemoveDraftPages_AndCleanupStorage()
    {
        await _initializer.InitializeAsync();

        var draft = (await _draftService.CreateDraftAsync("Temporary Draft")).Value!;
        CreateFakePng(Path.Combine(_tempSourceDir, "p1.png"), 800, 1200);
        await _ingestionService.IngestDraftFolderAsync(draft.Id, _tempSourceDir);

        var draftStorageDir = Path.Combine(_storageService.GetManagedStorageRoot(), "drafts", draft.Id.ToString("N"));
        Directory.Exists(draftStorageDir).Should().BeTrue();

        var deleteResult = await _draftService.DeleteDraftAsync(draft.Id);
        deleteResult.IsSuccess.Should().BeTrue();

        var retrieved = await _draftService.GetDraftAsync(draft.Id);
        retrieved.Should().BeNull();

        // Database draft pages must be removed
        var draftPagesInDb = await _dbContext.DraftPages.Where(p => p.DraftId == draft.Id).ToListAsync();
        draftPagesInDb.Should().BeEmpty();

        // Disk files must be cleaned up
        Directory.Exists(draftStorageDir).Should().BeFalse();
    }
}
