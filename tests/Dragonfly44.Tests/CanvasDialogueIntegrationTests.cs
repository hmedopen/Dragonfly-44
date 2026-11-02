using Dragonfly44.Application.Dialogue;
using Dragonfly44.Application.Rendering;
using Dragonfly44.Domain.Entities;
using Dragonfly44.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Dragonfly44.Tests;

public class CanvasDialogueIntegrationTests
{
    [Fact]
    public void SelectDialogueCard_ShouldTriggerCanvasPageJumpAndHighlightAssociatedBubble()
    {
        // Arrange
        var canvasSession = new CanvasNavigationSession();
        var feedSession = new DialogueFeedSession();

        var page1Id = Guid.NewGuid();
        var page2Id = Guid.NewGuid();

        var pages = new List<MangaPageLayoutInfo>
        {
            new(page1Id, 1, "001.png", "pages/001.png", "PNG", 1000f, 1500f),
            new(page2Id, 2, "002.png", "pages/002.png", "PNG", 1000f, 1500f)
        };
        canvasSession.LoadPages(pages, "Chapter 1", 0);

        var bubbleP1 = new Bubble { Id = Guid.NewGuid(), PageId = page1Id, ReadingOrderIndex = 1, RawText = "P1" };
        var bubbleP2 = new Bubble { Id = Guid.NewGuid(), PageId = page2Id, ReadingOrderIndex = 1, RawText = "P2" };

        var pageMap = new Dictionary<Guid, int>
        {
            [page1Id] = 1,
            [page2Id] = 2
        };
        feedSession.Load(new[] { bubbleP1, bubbleP2 }, pageMap);

        // Wire bidirectional navigation hook (same as MainViewModel)
        feedSession.NavigateToPageRequested += (s, pageNum) =>
        {
            canvasSession.SetPage(pageNum - 1);
        };

        canvasSession.CurrentPageNumber.Should().Be(1);

        // Act: select dialogue card belonging to Page 2
        feedSession.SelectById(bubbleP2.Id);

        // Assert: canvas automatically navigates to Page 2
        canvasSession.CurrentPageNumber.Should().Be(2);
        feedSession.SelectedItem.Should().NotBeNull();
        feedSession.SelectedItem!.Id.Should().Be(bubbleP2.Id);
        feedSession.SelectedItem.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void SelectBubbleOnCanvas_ShouldSelectCorrespondingDialogueCardInFeed()
    {
        // Arrange
        var feedSession = new DialogueFeedSession();
        var pageId = Guid.NewGuid();

        var b1 = new Bubble { Id = Guid.NewGuid(), PageId = pageId, ReadingOrderIndex = 1, RawText = "Bubble 1" };
        var b2 = new Bubble { Id = Guid.NewGuid(), PageId = pageId, ReadingOrderIndex = 2, RawText = "Bubble 2" };

        var pageMap = new Dictionary<Guid, int> { [pageId] = 1 };
        feedSession.Load(new[] { b1, b2 }, pageMap);

        // Act: canvas bubble hit-tested and selected
        feedSession.SelectById(b2.Id);

        // Assert
        feedSession.SelectedItem.Should().NotBeNull();
        feedSession.SelectedItem!.Id.Should().Be(b2.Id);
        feedSession.SelectedItem.RawText.Should().Be("Bubble 2");
        feedSession.SelectedItem.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void LargeDialogueFeed_PerformanceAndIntegrityTest()
    {
        // Arrange: 50 pages with 6 bubbles each = 300 dialogues
        var feedSession = new DialogueFeedSession();
        var bubbles = new List<Bubble>();
        var pageMap = new Dictionary<Guid, int>();

        for (int p = 1; p <= 50; p++)
        {
            var pageId = Guid.NewGuid();
            pageMap[pageId] = p;

            for (int b = 1; b <= 6; b++)
            {
                bubbles.Add(new Bubble
                {
                    Id = Guid.NewGuid(),
                    PageId = pageId,
                    ReadingOrderIndex = b,
                    RawText = $"Page {p} Bubble {b}",
                    TranslatedText = $"ص {p} فقاعة {b}",
                    ShapeType = BubbleShapeType.StandardSpeech,
                    IsFlagged = (p % 5 == 0) // Every 5th page flagged
                });
            }
        }

        // Act & Assert 1: Load 300 cards
        var sw = System.Diagnostics.Stopwatch.StartNew();
        feedSession.Load(bubbles, pageMap);
        sw.Stop();

        feedSession.TotalCount.Should().Be(300);
        feedSession.PageGroups.Should().HaveCount(50);
        sw.ElapsedMilliseconds.Should().BeLessThan(100);

        // Act & Assert 2: Filter to Flagged
        sw.Restart();
        feedSession.Filter = DialogueFeedFilter.Flagged;
        sw.Stop();

        feedSession.VisibleCards.Should().HaveCount(60); // 10 pages * 6 bubbles
        feedSession.VisibleCards[0].DisplayNumber.Should().Be(1);
        feedSession.VisibleCards[59].DisplayNumber.Should().Be(60);
        sw.ElapsedMilliseconds.Should().BeLessThan(20);

        // Act & Assert 3: Rapid selection traversal
        sw.Restart();
        for (int i = 0; i < 60; i++)
        {
            feedSession.SelectNext();
        }
        sw.Stop();

        feedSession.SelectedItem.Should().NotBeNull();
        feedSession.SelectedItem!.DisplayNumber.Should().Be(60);
        sw.ElapsedMilliseconds.Should().BeLessThan(20);
    }
}
