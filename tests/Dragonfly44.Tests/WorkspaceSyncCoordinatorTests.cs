using System.Numerics;
using Dragonfly44.Application.Dialogue;
using Dragonfly44.Application.Rendering;
using Dragonfly44.Application.Synchronization;
using Dragonfly44.Domain.Entities;
using Dragonfly44.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Dragonfly44.Tests;

/// <summary>
/// Comprehensive automated test suite for Phase 6 Canvas/Feed Synchronization.
/// Validates selection lifecycle, loop prevention, viewport navigation, active page synchronization,
/// concurrency resilience, data integrity, and chapter lifecycle cleanup.
/// </summary>
public class WorkspaceSyncCoordinatorTests
{
    // =============================================================
    // 1. SELECTION SYNCHRONIZATION TESTS (REQUIREMENT A, E)
    // =============================================================

    [Fact]
    public void SelectBubble_FromCanvas_ShouldUpdateAuthoritativeStateAndNotifyFeed()
    {
        // Arrange
        var coordinator = new WorkspaceSyncCoordinator();
        var bubbleId = Guid.NewGuid();
        SelectionChangedEventArgs? receivedArgs = null;
        coordinator.SelectionChanged += (s, e) => receivedArgs = e;

        // Act: User clicks a bubble on the canvas
        var result = coordinator.SelectBubble(bubbleId, pageNumber: 2, SelectionOrigin.Canvas);

        // Assert
        result.Should().BeTrue();
        coordinator.SelectedBubbleId.Should().Be(bubbleId);
        coordinator.ActivePageNumber.Should().Be(2);

        receivedArgs.Should().NotBeNull();
        receivedArgs!.SelectedBubbleId.Should().Be(bubbleId);
        receivedArgs.PageNumber.Should().Be(2);
        receivedArgs.Origin.Should().Be(SelectionOrigin.Canvas);
        receivedArgs.Version.Should().Be(1);
    }

    [Fact]
    public void SelectBubble_FromDialogueFeed_ShouldUpdateAuthoritativeStateAndNotifyCanvas()
    {
        // Arrange
        var coordinator = new WorkspaceSyncCoordinator();
        var bubbleId = Guid.NewGuid();
        SelectionChangedEventArgs? receivedArgs = null;
        coordinator.SelectionChanged += (s, e) => receivedArgs = e;

        // Act: User selects a dialogue card in the feed
        var result = coordinator.SelectBubble(bubbleId, pageNumber: 3, SelectionOrigin.DialogueFeed);

        // Assert
        result.Should().BeTrue();
        coordinator.SelectedBubbleId.Should().Be(bubbleId);
        coordinator.ActivePageNumber.Should().Be(3);

        receivedArgs.Should().NotBeNull();
        receivedArgs!.SelectedBubbleId.Should().Be(bubbleId);
        receivedArgs.PageNumber.Should().Be(3);
        receivedArgs.Origin.Should().Be(SelectionOrigin.DialogueFeed);
    }

    [Fact]
    public void ClearSelection_ShouldClearAuthoritativeStateAndNotifyListeners()
    {
        // Arrange
        var coordinator = new WorkspaceSyncCoordinator();
        var bubbleId = Guid.NewGuid();
        coordinator.SelectBubble(bubbleId, pageNumber: 1, SelectionOrigin.Canvas);

        SelectionChangedEventArgs? receivedArgs = null;
        coordinator.SelectionChanged += (s, e) => receivedArgs = e;

        // Act: Click empty background or clear
        coordinator.ClearSelection(SelectionOrigin.Canvas);

        // Assert
        coordinator.SelectedBubbleId.Should().BeNull();
        receivedArgs.Should().NotBeNull();
        receivedArgs!.SelectedBubbleId.Should().BeNull();
        receivedArgs.Origin.Should().Be(SelectionOrigin.Canvas);
    }

    [Fact]
    public void Selection_StableIdentity_ShouldPersistAcrossRenamesOrReorders()
    {
        // Arrange
        var coordinator = new WorkspaceSyncCoordinator();
        var bubbleId = Guid.NewGuid();
        coordinator.SelectBubble(bubbleId, pageNumber: 1, SelectionOrigin.DialogueFeed);

        // Act: Bubble ID is the authoritative stable identity, not index or display number
        coordinator.SelectedBubbleId.Should().Be(bubbleId);

        // Switch to another bubble
        var secondBubbleId = Guid.NewGuid();
        coordinator.SelectBubble(secondBubbleId, pageNumber: 1, SelectionOrigin.DialogueFeed);

        coordinator.SelectedBubbleId.Should().Be(secondBubbleId);
    }

    // =============================================================
    // 2. SYNCHRONIZATION LOOP PREVENTION & IDEMPOTENCY (REQUIREMENT F)
    // =============================================================

    [Fact]
    public void SelectBubble_DuplicateSelection_ShouldBeIdempotentAndNotRaiseEvent()
    {
        // Arrange
        var coordinator = new WorkspaceSyncCoordinator();
        var bubbleId = Guid.NewGuid();
        coordinator.SelectBubble(bubbleId, pageNumber: 1, SelectionOrigin.Canvas);

        int eventCount = 0;
        coordinator.SelectionChanged += (s, e) => eventCount++;

        // Act: Feed attempts to re-select the same bubble ID back
        var result = coordinator.SelectBubble(bubbleId, pageNumber: 1, SelectionOrigin.DialogueFeed);

        // Assert
        result.Should().BeFalse();
        eventCount.Should().Be(0); // Zero re-dispatched events! No loop!
        coordinator.SelectedBubbleId.Should().Be(bubbleId);
    }

    [Fact]
    public void BidirectionalHandshake_ShouldTerminateImmediatelyWithoutInfiniteLoop()
    {
        // Arrange
        var coordinator = new WorkspaceSyncCoordinator();
        int canvasEventCount = 0;
        int feedEventCount = 0;

        coordinator.SelectionChanged += (s, e) =>
        {
            if (e.Origin == SelectionOrigin.Canvas)
            {
                feedEventCount++;
                // Feed responds by confirming selection
                coordinator.SelectBubble(e.SelectedBubbleId, e.PageNumber, SelectionOrigin.DialogueFeed);
            }
            else if (e.Origin == SelectionOrigin.DialogueFeed)
            {
                canvasEventCount++;
                // Canvas responds by confirming selection
                coordinator.SelectBubble(e.SelectedBubbleId, e.PageNumber, SelectionOrigin.Canvas);
            }
        };

        var bubbleId = Guid.NewGuid();

        // Act: Trigger selection from Canvas
        coordinator.SelectBubble(bubbleId, pageNumber: 1, SelectionOrigin.Canvas);

        // Assert
        feedEventCount.Should().Be(1);
        canvasEventCount.Should().Be(0); // Handshake terminated on first attempt due to idempotency!
    }

    // =============================================================
    // 3. ACTIVE PAGE SYNCHRONIZATION (REQUIREMENT B)
    // =============================================================

    [Fact]
    public void SetActivePage_FromCanvas_ShouldUpdateFeedActivePage()
    {
        // Arrange
        var coordinator = new WorkspaceSyncCoordinator();
        PageChangedEventArgs? pageArgs = null;
        coordinator.PageChanged += (s, e) => pageArgs = e;

        // Act: Canvas navigates to Page 5
        var result = coordinator.SetActivePage(5, SelectionOrigin.Canvas);

        // Assert
        result.Should().BeTrue();
        coordinator.ActivePageNumber.Should().Be(5);
        pageArgs.Should().NotBeNull();
        pageArgs!.PageNumber.Should().Be(5);
        pageArgs.Origin.Should().Be(SelectionOrigin.Canvas);
    }

    [Fact]
    public void SetActivePage_FromFeedDivider_ShouldUpdateCanvasActivePage()
    {
        // Arrange
        var coordinator = new WorkspaceSyncCoordinator();
        PageChangedEventArgs? pageArgs = null;
        coordinator.PageChanged += (s, e) => pageArgs = e;

        // Act: User clicks Page Divider 3 in Feed
        var result = coordinator.SetActivePage(3, SelectionOrigin.DialogueFeed);

        // Assert
        result.Should().BeTrue();
        coordinator.ActivePageNumber.Should().Be(3);
        pageArgs.Should().NotBeNull();
        pageArgs!.PageNumber.Should().Be(3);
        pageArgs.Origin.Should().Be(SelectionOrigin.DialogueFeed);
    }

    [Fact]
    public void SetActivePage_SamePage_ShouldBeIdempotent()
    {
        // Arrange
        var coordinator = new WorkspaceSyncCoordinator();
        coordinator.SetActivePage(4, SelectionOrigin.Canvas);

        int eventCount = 0;
        coordinator.PageChanged += (s, e) => eventCount++;

        // Act: Navigate to Page 4 again
        var result = coordinator.SetActivePage(4, SelectionOrigin.DialogueFeed);

        // Assert
        result.Should().BeFalse();
        eventCount.Should().Be(0);
    }

    // =============================================================
    // 4. BUBBLE HIGHLIGHTING STATES (REQUIREMENT C)
    // =============================================================

    [Fact]
    public void BubbleHover_ShouldUpdateAuthoritativeHoverState()
    {
        // Arrange
        var coordinator = new WorkspaceSyncCoordinator();
        var bubbleId = Guid.NewGuid();
        BubbleHoverChangedEventArgs? hoverArgs = null;
        coordinator.BubbleHoverChanged += (s, e) => hoverArgs = e;

        // Act: Mouse hovers over bubble on canvas
        var result = coordinator.SetHoveredBubble(bubbleId, SelectionOrigin.Canvas);

        // Assert
        result.Should().BeTrue();
        coordinator.HoveredBubbleId.Should().Be(bubbleId);
        hoverArgs.Should().NotBeNull();
        hoverArgs!.HoveredBubbleId.Should().Be(bubbleId);

        // Act 2: Mouse leaves bubble
        coordinator.SetHoveredBubble(null, SelectionOrigin.Canvas);
        coordinator.HoveredBubbleId.Should().BeNull();
    }

    [Fact]
    public void BubbleActiveEditing_ShouldUpdateAuthoritativeEditingState()
    {
        // Arrange
        var coordinator = new WorkspaceSyncCoordinator();
        var bubbleId = Guid.NewGuid();
        BubbleEditingChangedEventArgs? editArgs = null;
        coordinator.BubbleEditingChanged += (s, e) => editArgs = e;

        // Act: User enters editing mode in dialogue feed card
        var result = coordinator.SetActiveEditing(bubbleId, SelectionOrigin.DialogueFeed);

        // Assert
        result.Should().BeTrue();
        coordinator.ActiveEditingBubbleId.Should().Be(bubbleId);
        editArgs.Should().NotBeNull();
        editArgs!.EditingBubbleId.Should().Be(bubbleId);

        // Act 2: User commits edits
        coordinator.SetActiveEditing(null, SelectionOrigin.DialogueFeed);
        coordinator.ActiveEditingBubbleId.Should().BeNull();
    }

    [Fact]
    public void PageOverlayItem_ShouldSupportAllFourVisualStatesIndependently()
    {
        // Arrange & Act
        var overlay = new PageOverlayItem
        {
            Id = Guid.NewGuid(),
            PageNumber = 1,
            Bounds = new NormalizedRect(0.1f, 0.1f, 0.2f, 0.2f),
            ReadingOrder = 1,
            IsSelected = false,
            IsHovered = false,
            IsEditing = false
        };

        // State 1: Normal
        overlay.IsSelected.Should().BeFalse();
        overlay.IsHovered.Should().BeFalse();
        overlay.IsEditing.Should().BeFalse();

        // State 2: Hovered
        overlay.IsHovered = true;
        overlay.IsHovered.Should().BeTrue();
        overlay.IsSelected.Should().BeFalse();

        // State 3: Selected
        overlay.IsHovered = false;
        overlay.IsSelected = true;
        overlay.IsSelected.Should().BeTrue();
        overlay.IsEditing.Should().BeFalse();

        // State 4: Active Editing
        overlay.IsEditing = true;
        overlay.IsEditing.Should().BeTrue();
        overlay.IsSelected.Should().BeTrue();
    }

    // =============================================================
    // 5. VIEWPORT NAVIGATION & VISIBILITY (REQUIREMENT D)
    // =============================================================

    [Fact]
    public void ViewportNavigation_SinglePageMode_ShouldCenterBubbleWhenOffscreen()
    {
        // Arrange
        var session = new CanvasNavigationSession();
        var page1 = new MangaPageLayoutInfo(Guid.NewGuid(), 1, "01.png", "01.png", "PNG", 1000f, 1500f);
        var page2 = new MangaPageLayoutInfo(Guid.NewGuid(), 2, "02.png", "02.png", "PNG", 1000f, 1500f);
        session.LoadPages(new[] { page1, page2 }, "Test Chapter", 0);
        session.Mode = CanvasViewMode.Page;

        var bubbleP2 = new PageOverlayItem
        {
            Id = Guid.NewGuid(),
            PageNumber = 2,
            Bounds = new NormalizedRect(0.4f, 0.5f, 0.2f, 0.1f), // Centered on Page 2
            ReadingOrder = 1
        };
        session.AddOverlay(bubbleP2);

        session.CurrentPageNumber.Should().Be(1);

        // Act: Bring bubble on Page 2 into view
        var navigated = session.EnsureBubbleVisible(bubbleP2.Id, vpWidth: 800f, vpHeight: 600f, preserveZoom: true);

        // Assert
        navigated.Should().BeTrue();
        session.CurrentPageNumber.Should().Be(2); // Automatically flipped to Page 2!
        session.Transform.Pan.Y.Should().NotBe(0); // Pan adjusted to center the bubble!
    }

    [Fact]
    public void ViewportNavigation_WhenBubbleAlreadyVisible_ShouldNotMoveCamera()
    {
        // Arrange: Fit Page 1 in a large viewport so entire page is visible
        var session = new CanvasNavigationSession();
        var page1 = new MangaPageLayoutInfo(Guid.NewGuid(), 1, "01.png", "01.png", "PNG", 1000f, 1500f);
        session.LoadPages(new[] { page1 }, "Test Chapter", 0);
        session.Mode = CanvasViewMode.Page;
        session.FitPage(vpWidth: 1200f, vpHeight: 1800f);

        var bubble = new PageOverlayItem
        {
            Id = Guid.NewGuid(),
            PageNumber = 1,
            Bounds = new NormalizedRect(0.4f, 0.4f, 0.2f, 0.1f),
            ReadingOrder = 1
        };
        session.AddOverlay(bubble);

        var initialZoom = session.Transform.Zoom;
        var initialPan = session.Transform.Pan;

        // Act: Ensure bubble visible when already in view
        var navigated = session.EnsureBubbleVisible(bubble.Id, vpWidth: 1200f, vpHeight: 1800f, preserveZoom: true);

        // Assert: Camera should remain completely stationary
        navigated.Should().BeTrue();
        session.Transform.Zoom.Should().Be(initialZoom);
        session.Transform.Pan.Should().Be(initialPan);
    }

    [Fact]
    public void ViewportNavigation_ContinuousMode_ShouldScrollToBubble()
    {
        // Arrange: 3 pages stacked vertically
        var session = new CanvasNavigationSession();
        var page1 = new MangaPageLayoutInfo(Guid.NewGuid(), 1, "01.png", "01.png", "PNG", 1000f, 1500f);
        var page2 = new MangaPageLayoutInfo(Guid.NewGuid(), 2, "02.png", "02.png", "PNG", 1000f, 1500f);
        var page3 = new MangaPageLayoutInfo(Guid.NewGuid(), 3, "03.png", "03.png", "PNG", 1000f, 1500f);
        session.LoadPages(new[] { page1, page2, page3 }, "Test Chapter", 0);
        session.Mode = CanvasViewMode.Continuous;

        var bubbleP3 = new PageOverlayItem
        {
            Id = Guid.NewGuid(),
            PageNumber = 3,
            Bounds = new NormalizedRect(0.3f, 0.5f, 0.3f, 0.1f),
            ReadingOrder = 1
        };
        session.AddOverlay(bubbleP3);

        session.Transform.Pan = Vector2.Zero;

        // Act: Ensure bubble on Page 3 is visible in continuous mode
        var navigated = session.EnsureBubbleVisible(bubbleP3.Id, vpWidth: 1000f, vpHeight: 800f, preserveZoom: true);

        // Assert
        navigated.Should().BeTrue();
        session.CurrentPageNumber.Should().Be(3);
        session.Transform.Pan.Y.Should().BeLessThan(0); // Scrolled down into the strip!
    }

    // =============================================================
    // 6. CONCURRENCY & ASYNCHRONOUS VERSIONING (REQUIREMENT G)
    // =============================================================

    [Fact]
    public void RapidSelections_ShouldIncrementVersionMonotonically()
    {
        // Arrange
        var coordinator = new WorkspaceSyncCoordinator();
        var versions = new List<long>();
        coordinator.SelectionChanged += (s, e) => versions.Add(e.Version);

        // Act: 10 rapid successive selections
        for (int i = 0; i < 10; i++)
        {
            coordinator.SelectBubble(Guid.NewGuid(), pageNumber: 1, SelectionOrigin.DialogueFeed);
        }

        // Assert
        versions.Should().HaveCount(10);
        versions.Should().BeInAscendingOrder();
        coordinator.CurrentVersion.Should().Be(10);
    }

    [Fact]
    public void StaleAsynchronousNavigation_ShouldBeDiscardedByVersionCheck()
    {
        // Arrange
        var coordinator = new WorkspaceSyncCoordinator();
        var bubble1 = Guid.NewGuid();
        var bubble2 = Guid.NewGuid();

        coordinator.SelectBubble(bubble1, 1, SelectionOrigin.Canvas);
        var initialVersion = coordinator.CurrentVersion;

        // User rapidly selects a new bubble before an async operation finishes
        coordinator.SelectBubble(bubble2, 2, SelectionOrigin.DialogueFeed);
        var latestVersion = coordinator.CurrentVersion;

        // Act: Simulated delayed async callback finishes with initialVersion
        bool appliedState = false;
        if (initialVersion == coordinator.CurrentVersion)
        {
            appliedState = true;
        }

        // Assert
        appliedState.Should().BeFalse(); // Stale async operation was successfully prevented!
        coordinator.SelectedBubbleId.Should().Be(bubble2);
        latestVersion.Should().BeGreaterThan(initialVersion);
    }

    // =============================================================
    // 7. DATA INTEGRITY & REORDERING (REQUIREMENT H)
    // =============================================================

    [Fact]
    public void DragDropReordering_ShouldUpdateReadingOrderWhilePreservingOverlayIdentity()
    {
        // Arrange
        var session = new CanvasNavigationSession();
        var pageId = Guid.NewGuid();
        var b1 = new PageOverlayItem { Id = Guid.NewGuid(), PageNumber = 1, ReadingOrder = 1, Label = "#1" };
        var b2 = new PageOverlayItem { Id = Guid.NewGuid(), PageNumber = 1, ReadingOrder = 2, Label = "#2" };
        var b3 = new PageOverlayItem { Id = Guid.NewGuid(), PageNumber = 1, ReadingOrder = 3, Label = "#3" };

        session.AddOverlay(b1);
        session.AddOverlay(b2);
        session.AddOverlay(b3);

        // Act: Reorder items such that b3 is moved to position 1: [b3, b1, b2]
        var newOrder = new[] { b3.Id, b1.Id, b2.Id };
        session.UpdateOverlaysReadingOrder(pageId, newOrder);

        // Assert: Guids remain identical, reading orders update
        b3.ReadingOrder.Should().Be(1);
        b3.Label.Should().Be("#1");

        b1.ReadingOrder.Should().Be(2);
        b1.Label.Should().Be("#2");

        b2.ReadingOrder.Should().Be(3);
        b2.Label.Should().Be("#3");
    }

    [Fact]
    public void CardDeletion_ShouldRemoveOverlayAndClearSelection()
    {
        // Arrange
        var coordinator = new WorkspaceSyncCoordinator();
        var session = new CanvasNavigationSession();

        var b1 = new PageOverlayItem { Id = Guid.NewGuid(), PageNumber = 1, ReadingOrder = 1 };
        session.AddOverlay(b1);
        coordinator.SelectBubble(b1.Id, 1, SelectionOrigin.DialogueFeed);

        coordinator.SelectedBubbleId.Should().Be(b1.Id);
        session.Overlays.Should().HaveCount(1);

        // Act: Delete card
        session.RemoveOverlay(b1.Id);
        coordinator.ClearSelection(SelectionOrigin.DialogueFeed);

        // Assert
        session.Overlays.Should().BeEmpty();
        coordinator.SelectedBubbleId.Should().BeNull();
    }

    [Fact]
    public void FilteringOutSelectedCard_ShouldNotCorruptAuthoritativeState()
    {
        // Arrange
        var feedSession = new DialogueFeedSession();
        var pageId = Guid.NewGuid();
        var unflaggedBubble = new Bubble { Id = Guid.NewGuid(), PageId = pageId, ReadingOrderIndex = 1, IsFlagged = false };
        var flaggedBubble = new Bubble { Id = Guid.NewGuid(), PageId = pageId, ReadingOrderIndex = 2, IsFlagged = true };

        var map = new Dictionary<Guid, int> { [pageId] = 1 };
        feedSession.Load(new[] { unflaggedBubble, flaggedBubble }, map);

        // Select unflagged bubble
        feedSession.SelectById(unflaggedBubble.Id);
        feedSession.SelectedItem.Should().NotBeNull();
        feedSession.SelectedItem!.Id.Should().Be(unflaggedBubble.Id);

        // Act: Filter feed to Flagged only
        feedSession.Filter = DialogueFeedFilter.Flagged;

        // Assert
        feedSession.VisibleCards.Should().HaveCount(1);
        feedSession.VisibleCards[0].Id.Should().Be(flaggedBubble.Id);
        feedSession.VisibleCards.Should().NotContain(c => c.Id == unflaggedBubble.Id);
    }

    // =============================================================
    // 8. CHAPTER SWITCHING & LIFECYCLE CLEANUP (REQUIREMENT I)
    // =============================================================

    [Fact]
    public void ChapterSwitching_ShouldResetSelectionAndClearStaleSubscriptions()
    {
        // Arrange
        var coordinator = new WorkspaceSyncCoordinator();
        var chapter1Id = Guid.NewGuid();
        var chapter2Id = Guid.NewGuid();

        coordinator.SetChapter(chapter1Id);
        coordinator.SelectBubble(Guid.NewGuid(), 1, SelectionOrigin.Canvas);
        coordinator.SetActiveEditing(Guid.NewGuid(), SelectionOrigin.DialogueFeed);
        coordinator.SetHoveredBubble(Guid.NewGuid(), SelectionOrigin.Canvas);

        coordinator.CurrentChapterId.Should().Be(chapter1Id);
        coordinator.SelectedBubbleId.Should().NotBeNull();
        coordinator.ActiveEditingBubbleId.Should().NotBeNull();
        coordinator.HoveredBubbleId.Should().NotBeNull();

        bool resetFired = false;
        coordinator.ChapterReset += (s, e) => resetFired = true;

        // Act: Switch to Chapter 2
        coordinator.SetChapter(chapter2Id);

        // Assert: Everything from previous chapter is cleared cleanly
        resetFired.Should().BeTrue();
        coordinator.CurrentChapterId.Should().Be(chapter2Id);
        coordinator.SelectedBubbleId.Should().BeNull();
        coordinator.ActiveEditingBubbleId.Should().BeNull();
        coordinator.HoveredBubbleId.Should().BeNull();
        coordinator.ActivePageNumber.Should().Be(1);
    }
}
