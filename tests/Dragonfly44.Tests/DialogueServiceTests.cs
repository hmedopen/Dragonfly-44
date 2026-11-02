using Dragonfly44.Application.Services;
using Dragonfly44.Domain.Entities;
using Dragonfly44.Domain.Enums;
using Dragonfly44.Infrastructure.Configuration;
using Dragonfly44.Infrastructure.Persistence;
using Dragonfly44.Infrastructure.Persistence.Repositories;
using Dragonfly44.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dragonfly44.Tests;

public class DialogueServiceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _tempStorageDir;
    private readonly Dragonfly44DbContext _dbContext;
    private readonly SqliteDatabaseInitializer _initializer;
    private readonly BubbleRepository _bubbleRepository;
    private readonly PageRepository _pageRepository;
    private readonly DialogueService _dialogueService;

    private readonly Project _project;
    private readonly Chapter _chapter;
    private readonly Page _page1;
    private readonly Page _page2;

    public DialogueServiceTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"dragonfly44_diag_test_{Guid.NewGuid():N}.db");
        _tempStorageDir = Path.Combine(Path.GetTempPath(), $"dragonfly44_diag_storage_{Guid.NewGuid():N}");
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

        _initializer.InitializeAsync().GetAwaiter().GetResult();

        _bubbleRepository = new BubbleRepository(_dbContext);
        _pageRepository = new PageRepository(_dbContext);
        _dialogueService = new DialogueService(_bubbleRepository, _pageRepository, NullLogger<DialogueService>.Instance);

        // Seed project, chapter, and two pages
        _project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Test Manga",
            SourceLanguage = "ja",
            TargetLanguage = "ar",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _dbContext.Projects.Add(_project);

        _chapter = new Chapter
        {
            Id = Guid.NewGuid(),
            ProjectId = _project.Id,
            ChapterNumber = 1,
            Title = "Chapter 1",
            Status = ChapterStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _dbContext.Chapters.Add(_chapter);

        _page1 = new Page
        {
            Id = Guid.NewGuid(),
            ChapterId = _chapter.Id,
            PageNumber = 1,
            ImageRelativePath = "pages/001.png",
            Width = 1000,
            Height = 1500,
            Status = PageStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };
        _page2 = new Page
        {
            Id = Guid.NewGuid(),
            ChapterId = _chapter.Id,
            PageNumber = 2,
            ImageRelativePath = "pages/002.png",
            Width = 1000,
            Height = 1500,
            Status = PageStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };
        _dbContext.Pages.AddRange(_page1, _page2);
        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
        if (Directory.Exists(_tempStorageDir))
        {
            try { Directory.Delete(_tempStorageDir, true); } catch { }
        }
    }

    [Fact]
    public async Task CreateDialogueAsync_ShouldPersistBubbleWithSequentialReadingOrderAndPreserveIdentity()
    {
        // Act
        var res1 = await _dialogueService.CreateDialogueAsync(_page1.Id, "Hello", "مرحبا", BubbleShapeType.StandardSpeech);
        var res2 = await _dialogueService.CreateDialogueAsync(_page1.Id, "World", "عالم", BubbleShapeType.Shout);

        // Assert
        res1.IsSuccess.Should().BeTrue();
        var b1 = res1.Value!;
        b1.PageId.Should().Be(_page1.Id);
        b1.ReadingOrderIndex.Should().Be(1);
        b1.RawText.Should().Be("Hello");
        b1.TranslatedText.Should().Be("مرحبا");
        b1.ShapeType.Should().Be(BubbleShapeType.StandardSpeech);
        b1.IsFlagged.Should().BeFalse();

        res2.IsSuccess.Should().BeTrue();
        var b2 = res2.Value!;
        b2.PageId.Should().Be(_page1.Id);
        b2.ReadingOrderIndex.Should().Be(2);
        b2.RawText.Should().Be("World");
        b2.TranslatedText.Should().Be("عالم");
        b2.ShapeType.Should().Be(BubbleShapeType.Shout);

        // Verify direct from database
        var saved1 = await _bubbleRepository.GetByIdAsync(b1.Id);
        saved1.Should().NotBeNull();
        saved1!.Id.Should().Be(b1.Id);
        saved1.TranslatedText.Should().Be("مرحبا");
    }

    [Fact]
    public async Task UpdateDialogueTextAsync_ShouldUpdateSourceAndTranslation_AndPreserveArabicAndDiacritics()
    {
        // Arrange
        var res = await _dialogueService.CreateDialogueAsync(_page1.Id, "Old", "قديم", BubbleShapeType.StandardSpeech);
        res.IsSuccess.Should().BeTrue();
        var bubble = res.Value!;
        var arabicTextWithTashkeel = "هَلْ أَنْتَ بِخَيْرٍ؟ نَعَمْ، أَنَا بِخَيْرٍ!";

        // Act
        var updateRes = await _dialogueService.UpdateDialogueTextAsync(bubble.Id, "New Source", arabicTextWithTashkeel);

        // Assert
        updateRes.IsSuccess.Should().BeTrue();
        var updated = updateRes.Value!;
        updated.RawText.Should().Be("New Source");
        updated.TranslatedText.Should().Be(arabicTextWithTashkeel);

        var retrieved = await _bubbleRepository.GetByIdAsync(bubble.Id);
        retrieved!.TranslatedText.Should().Be(arabicTextWithTashkeel);
        retrieved.TranslatedText.Should().Contain("ْ");
        retrieved.TranslatedText.Should().Contain("ٍ");
    }

    [Fact]
    public async Task UpdateClassificationAsync_ShouldUpdateBubbleShapeType()
    {
        // Arrange
        var res = await _dialogueService.CreateDialogueAsync(_page1.Id, "Text", "نص", BubbleShapeType.StandardSpeech);
        res.IsSuccess.Should().BeTrue();
        var bubble = res.Value!;

        // Act
        var updateRes = await _dialogueService.UpdateClassificationAsync(bubble.Id, BubbleShapeType.Thought);

        // Assert
        updateRes.IsSuccess.Should().BeTrue();
        var updated = updateRes.Value!;
        updated.ShapeType.Should().Be(BubbleShapeType.Thought);

        var retrieved = await _bubbleRepository.GetByIdAsync(bubble.Id);
        retrieved!.ShapeType.Should().Be(BubbleShapeType.Thought);
    }

    [Fact]
    public async Task ToggleFlaggedAsync_ShouldToggleFlagStateAndPersist()
    {
        // Arrange
        var res = await _dialogueService.CreateDialogueAsync(_page1.Id, "Text", "نص", BubbleShapeType.StandardSpeech);
        res.IsSuccess.Should().BeTrue();
        var bubble = res.Value!;
        bubble.IsFlagged.Should().BeFalse();

        // Act 1: Toggle to true
        var flagTrueRes = await _dialogueService.ToggleFlaggedAsync(bubble.Id);
        flagTrueRes.IsSuccess.Should().BeTrue();
        flagTrueRes.Value!.IsFlagged.Should().BeTrue();

        var retrieved1 = await _bubbleRepository.GetByIdAsync(bubble.Id);
        retrieved1!.IsFlagged.Should().BeTrue();

        // Act 2: Toggle to false
        var flagFalseRes = await _dialogueService.ToggleFlaggedAsync(bubble.Id);
        flagFalseRes.IsSuccess.Should().BeTrue();
        flagFalseRes.Value!.IsFlagged.Should().BeFalse();

        var retrieved2 = await _bubbleRepository.GetByIdAsync(bubble.Id);
        retrieved2!.IsFlagged.Should().BeFalse();
    }

    [Fact]
    public async Task ReorderDialoguesAsync_ShouldUpdateReadingOrderIndices_AndPreserveGuids()
    {
        // Arrange: create 3 dialogues (index 0, 1, 2)
        var b0 = (await _dialogueService.CreateDialogueAsync(_page1.Id, "0", "٠", BubbleShapeType.StandardSpeech)).Value!;
        var b1 = (await _dialogueService.CreateDialogueAsync(_page1.Id, "1", "١", BubbleShapeType.StandardSpeech)).Value!;
        var b2 = (await _dialogueService.CreateDialogueAsync(_page1.Id, "2", "٢", BubbleShapeType.StandardSpeech)).Value!;

        // Act: reorder to [b2, b0, b1]
        var newOrder = new List<Guid> { b2.Id, b0.Id, b1.Id };
        var reorderRes = await _dialogueService.ReorderDialoguesAsync(_page1.Id, newOrder);
        reorderRes.IsSuccess.Should().BeTrue();

        // Query repository directly to verify persistence
        var fromDb = await _bubbleRepository.GetByPageIdAsync(_page1.Id);
        fromDb.Should().HaveCount(3);
        fromDb.Select(b => b.Id).Should().Equal(b2.Id, b0.Id, b1.Id);
        fromDb.Select(b => b.ReadingOrderIndex).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task DeleteDialogueAsync_ShouldRemoveBubble_AndResequenceRemainingBubblesWithoutGaps()
    {
        // Arrange: create 3 dialogues (1, 2, 3)
        var b0 = (await _dialogueService.CreateDialogueAsync(_page1.Id, "First", "الأول", BubbleShapeType.StandardSpeech)).Value!;
        var b1 = (await _dialogueService.CreateDialogueAsync(_page1.Id, "Second", "الثاني", BubbleShapeType.StandardSpeech)).Value!;
        var b2 = (await _dialogueService.CreateDialogueAsync(_page1.Id, "Third", "الثالث", BubbleShapeType.StandardSpeech)).Value!;

        // Act: delete the middle one (b1)
        var deleteResult = await _dialogueService.DeleteDialogueAsync(b1.Id);
        deleteResult.IsSuccess.Should().BeTrue();

        // Assert: b1 is gone
        var deleted = await _bubbleRepository.GetByIdAsync(b1.Id);
        deleted.Should().BeNull();

        // Remaining bubbles on page 1 should be re-indexed as 1 and 2
        var remaining = await _bubbleRepository.GetByPageIdAsync(_page1.Id);
        remaining.Should().HaveCount(2);
        remaining[0].Id.Should().Be(b0.Id);
        remaining[0].ReadingOrderIndex.Should().Be(1);

        remaining[1].Id.Should().Be(b2.Id);
        remaining[1].ReadingOrderIndex.Should().Be(2);
    }

    [Fact]
    public async Task GetDialogueByChapterAsync_ShouldOrderAcrossPagesByPageNumberAndReadingOrderIndex()
    {
        // Arrange
        var p1_b0 = (await _dialogueService.CreateDialogueAsync(_page1.Id, "P1-B0", "نص", BubbleShapeType.StandardSpeech)).Value!;
        var p1_b1 = (await _dialogueService.CreateDialogueAsync(_page1.Id, "P1-B1", "نص", BubbleShapeType.StandardSpeech)).Value!;
        var p2_b0 = (await _dialogueService.CreateDialogueAsync(_page2.Id, "P2-B0", "نص", BubbleShapeType.StandardSpeech)).Value!;
        var p2_b1 = (await _dialogueService.CreateDialogueAsync(_page2.Id, "P2-B1", "نص", BubbleShapeType.StandardSpeech)).Value!;

        // Act
        var res = await _dialogueService.GetDialogueByChapterAsync(_chapter.Id);

        // Assert
        res.IsSuccess.Should().BeTrue();
        var chapterBubbles = res.Value!;
        chapterBubbles.Should().HaveCount(4);
        chapterBubbles.Select(b => b.Id).Should().Equal(p1_b0.Id, p1_b1.Id, p2_b0.Id, p2_b1.Id);
    }
}
