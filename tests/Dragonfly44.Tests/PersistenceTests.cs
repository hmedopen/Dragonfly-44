using Dragonfly44.Domain.Contracts;
using Dragonfly44.Domain.Entities;
using Dragonfly44.Domain.Enums;
using Dragonfly44.Infrastructure.Configuration;
using Dragonfly44.Infrastructure.Persistence;
using Dragonfly44.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dragonfly44.Tests;

public class PersistenceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly Dragonfly44DbContext _dbContext;
    private readonly SqliteDatabaseInitializer _initializer;
    private readonly ProjectRepository _repository;

    public PersistenceTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"dragonfly44_test_{Guid.NewGuid():N}.db");

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
            AppDataDirectory = Path.GetDirectoryName(_testDbPath)!
        });

        _initializer = new SqliteDatabaseInitializer(
            _dbContext,
            dbOptions,
            storageOptions,
            NullLogger<SqliteDatabaseInitializer>.Instance);

        _repository = new ProjectRepository(_dbContext);
    }

    [Fact]
    public async Task InitializeAsync_ShouldCreateDatabaseAndTables()
    {
        // Act
        await _initializer.InitializeAsync();
        var canConnect = await _initializer.CanConnectAsync();

        // Assert
        canConnect.Should().BeTrue();
        File.Exists(_testDbPath).Should().BeTrue();
    }

    [Fact]
    public async Task FreshDatabase_AfterInitialization_ShouldHaveZeroProjectsByDefault()
    {
        // Act
        await _initializer.InitializeAsync();
        var projects = await _repository.GetAllAsync();

        // Assert: Database must be genuinely empty for new users without automatic sample data
        projects.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAndRetrieve_ProjectWithFullHierarchy_ShouldPersistCorrectly()
    {
        // Arrange
        await _initializer.InitializeAsync();

        var project = new Project
        {
            Name = "Berserk Deluxe",
            SourceLanguage = "ja",
            TargetLanguage = "ar",
            Description = "Dark fantasy test project"
        };

        var chapter = new Chapter
        {
            ProjectId = project.Id,
            ChapterNumber = 1,
            Title = "The Black Swordsman",
            Status = ChapterStatus.Translating
        };

        var chapterContext = new ChapterContext
        {
            ChapterId = chapter.Id,
            Summary = "Introduction of Guts in a tavern.",
            ToneGuidelines = "Gritty, archaic Arabic dialect.",
            CulturalNotes = "Translate medieval terms into classical Arabic.",
            ConsistencyRules = "Keep Dragonslayer untranslated or phonetically matched."
        };
        chapter.ChapterContext = chapterContext;

        var page = new Page
        {
            ChapterId = chapter.Id,
            PageNumber = 1,
            ImageRelativePath = "ch01/page01.png",
            Width = 1600,
            Height = 2400,
            Status = PageStatus.Scanning
        };

        var character = new Character
        {
            ProjectId = project.Id,
            CanonicalName = "Guts",
            LocalizedName = "غاتس",
            ColorHex = "#2B2B2B",
            Description = "The protagonist"
        };

        var bubble = new Bubble
        {
            PageId = page.Id,
            CharacterId = character.Id,
            X = 120.5,
            Y = 340.0,
            Width = 250.0,
            Height = 180.0,
            RawText = "オレの剣は重いぞ",
            TranslatedText = "سيفي ثقيل جداً",
            ReadingOrderIndex = 1,
            Confidence = 0.98,
            ShapeType = BubbleShapeType.StandardSpeech
        };

        var glossary = new GlossaryEntry
        {
            ProjectId = project.Id,
            SourceTerm = "Dragonslayer",
            TargetTerm = "قاتل التنانين",
            Category = "Weapons",
            ContextNotes = "Huge sword"
        };

        page.Bubbles.Add(bubble);
        chapter.Pages.Add(page);
        project.Chapters.Add(chapter);
        project.Characters.Add(character);
        project.GlossaryEntries.Add(glossary);

        // Act - Save
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await _repository.AddAsync(project, cts.Token);
        await _repository.SaveChangesAsync(cts.Token);

        // Act - Retrieve
        var loaded = await _repository.GetByIdAsync(project.Id, cts.Token);

        // Assert
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Berserk Deluxe");
        loaded.SourceLanguage.Should().Be("ja");
        loaded.TargetLanguage.Should().Be("ar");
        loaded.Chapters.Should().HaveCount(1);
        loaded.Characters.Should().HaveCount(1);
        loaded.GlossaryEntries.Should().HaveCount(1);

        var loadedChapter = loaded.Chapters[0];
        loadedChapter.Title.Should().Be("The Black Swordsman");
        loadedChapter.Pages.Should().HaveCount(1);

        var loadedPage = loadedChapter.Pages[0];
        loadedPage.Bubbles.Should().HaveCount(1);
        loadedPage.Bubbles[0].RawText.Should().Be("オレの剣は重いぞ");
        loadedPage.Bubbles[0].TranslatedText.Should().Be("سيفي ثقيل جداً");
    }

    [Fact]
    public async Task DeleteProject_ShouldCascadeDeleteChaptersAndPages()
    {
        // Arrange
        await _initializer.InitializeAsync();

        var project = new Project { Name = "Cascade Test Project" };
        var chapter = new Chapter { ProjectId = project.Id, ChapterNumber = 1, Title = "Ch 1" };
        var page = new Page { ChapterId = chapter.Id, PageNumber = 1, ImageRelativePath = "p1.png" };
        chapter.Pages.Add(page);
        project.Chapters.Add(chapter);

        await _repository.AddAsync(project);
        await _repository.SaveChangesAsync();

        // Act
        await _repository.DeleteAsync(project.Id);
        await _repository.SaveChangesAsync();

        // Assert
        var checkProject = await _repository.GetByIdAsync(project.Id);
        checkProject.Should().BeNull();

        var remainingPages = await _dbContext.Pages.Where(p => p.ChapterId == chapter.Id).ToListAsync();
        remainingPages.Should().BeEmpty();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        try
        {
            if (File.Exists(_testDbPath)) File.Delete(_testDbPath);
        }
        catch { }
    }
}
