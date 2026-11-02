using Dragonfly44.Application.Dialogue;
using Dragonfly44.Domain.Entities;
using Dragonfly44.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Dragonfly44.Tests;

public class DialogueFeedSessionTests
{
    private readonly Guid _page1Id = Guid.NewGuid();
    private readonly Guid _page2Id = Guid.NewGuid();
    private readonly Dictionary<Guid, int> _pageMap;
    private readonly List<Bubble> _testBubbles;

    public DialogueFeedSessionTests()
    {
        _pageMap = new Dictionary<Guid, int>
        {
            [_page1Id] = 1,
            [_page2Id] = 2
        };

        _testBubbles = new List<Bubble>
        {
            new Bubble { Id = Guid.NewGuid(), PageId = _page1Id, ReadingOrderIndex = 1, RawText = "P1-B1", TranslatedText = "صفحة ١ فقاعة ١", IsFlagged = false },
            new Bubble { Id = Guid.NewGuid(), PageId = _page1Id, ReadingOrderIndex = 2, RawText = "P1-B2", TranslatedText = "صفحة ١ فقاعة ٢", IsFlagged = true },
            new Bubble { Id = Guid.NewGuid(), PageId = _page2Id, ReadingOrderIndex = 1, RawText = "P2-B1", TranslatedText = "صفحة ٢ فقاعة ١", IsFlagged = false },
            new Bubble { Id = Guid.NewGuid(), PageId = _page2Id, ReadingOrderIndex = 2, RawText = "P2-B2", TranslatedText = "صفحة ٢ فقاعة ٢", IsFlagged = true }
        };
    }

    [Fact]
    public void Load_ShouldPopulateCardsAndPreserveDisplayNumberingAndPageGroups()
    {
        var session = new DialogueFeedSession();
        session.Load(_testBubbles, _pageMap);

        session.TotalCount.Should().Be(4);
        session.FlaggedCount.Should().Be(2);
        session.PageGroups.Should().HaveCount(2);

        session.PageGroups[0].PageNumber.Should().Be(1);
        session.PageGroups[0].Items.Should().HaveCount(2);
        session.PageGroups[1].PageNumber.Should().Be(2);
        session.PageGroups[1].Items.Should().HaveCount(2);

        session.VisibleCards.Should().HaveCount(4);
        session.VisibleCards[0].DisplayNumber.Should().Be(1);
        session.VisibleCards[0].DisplayNumberText.Should().Be("#1");
        session.VisibleCards[1].DisplayNumber.Should().Be(2);
        session.VisibleCards[2].DisplayNumber.Should().Be(3);
        session.VisibleCards[3].DisplayNumber.Should().Be(4);
    }

    [Fact]
    public void Filter_WhenSwitchingToFlagged_ShouldOnlyShowFlaggedItems_AndReindexDisplayNumbersWithoutModifyingGuids()
    {
        var session = new DialogueFeedSession();
        session.Load(_testBubbles, _pageMap);

        var originalCard2Id = session.VisibleCards[1].Id;
        var originalCard4Id = session.VisibleCards[3].Id;

        // Switch filter to Flagged
        session.Filter = DialogueFeedFilter.Flagged;

        session.VisibleCards.Should().HaveCount(2);
        session.VisibleCards[0].Id.Should().Be(originalCard2Id);
        session.VisibleCards[0].DisplayNumber.Should().Be(1, "Display number should be dynamic in filtered view");
        session.VisibleCards[0].DisplayNumberText.Should().Be("#1");

        session.VisibleCards[1].Id.Should().Be(originalCard4Id);
        session.VisibleCards[1].DisplayNumber.Should().Be(2);
        session.VisibleCards[1].DisplayNumberText.Should().Be("#2");

        // Switch back to All
        session.Filter = DialogueFeedFilter.All;

        session.VisibleCards.Should().HaveCount(4);
        session.VisibleCards[1].Id.Should().Be(originalCard2Id);
        session.VisibleCards[1].DisplayNumber.Should().Be(2);
        session.VisibleCards[3].Id.Should().Be(originalCard4Id);
        session.VisibleCards[3].DisplayNumber.Should().Be(4);
    }

    [Fact]
    public void Selection_SelectNextAndSelectPrevious_ShouldNavigateThroughVisibleCards()
    {
        var session = new DialogueFeedSession();
        session.Load(_testBubbles, _pageMap);

        // Initially no selection
        session.SelectedItem.Should().BeNull();

        // Select next -> selects card 1
        session.SelectNext();
        session.SelectedItem.Should().NotBeNull();
        session.SelectedItem!.DisplayNumber.Should().Be(1);

        // Select next -> selects card 2
        session.SelectNext();
        session.SelectedItem!.DisplayNumber.Should().Be(2);

        // Select previous -> back to card 1
        session.SelectPrevious();
        session.SelectedItem!.DisplayNumber.Should().Be(1);

        // Select previous again -> stays on card 1 (clamped)
        session.SelectPrevious();
        session.SelectedItem!.DisplayNumber.Should().Be(1);
    }

    [Fact]
    public void EditingState_Transitions_ShouldUpdateIsEditingAndIsSelected()
    {
        var session = new DialogueFeedSession();
        session.Load(_testBubbles, _pageMap);

        session.SelectNext(); // Selects card 1
        var item = session.SelectedItem!;
        item.IsSelected.Should().BeTrue();
        item.IsEditing.Should().BeFalse();

        // Start editing
        session.StartEditingSelected();
        item.IsEditing.Should().BeTrue();
        session.ActiveEditingItem.Should().Be(item);

        // Finish editing
        session.FinishEditing();
        item.IsEditing.Should().BeFalse();
        session.ActiveEditingItem.Should().BeNull();
        item.IsSelected.Should().BeTrue(); // Remains selected
    }

    [Fact]
    public void Reordering_MoveUpAndMoveDown_ShouldReorderWithinPageGroup_AndPreservePageAssociations()
    {
        var session = new DialogueFeedSession();
        session.Load(_testBubbles, _pageMap);

        var page1Group = session.PageGroups[0];
        var card1 = page1Group.Items[0];
        var card2 = page1Group.Items[1];

        // Card 1 cannot move up (already top of page 1)
        var movedUpTop = session.MoveUp(card1.Id);
        movedUpTop.Should().BeFalse();

        // Card 2 moves up
        var movedUp = session.MoveUp(card2.Id);
        movedUp.Should().BeTrue();

        page1Group.Items[0].Id.Should().Be(card2.Id);
        page1Group.Items[1].Id.Should().Be(card1.Id);

        // Card 2 moves back down
        var movedDown = session.MoveDown(card2.Id);
        movedDown.Should().BeTrue();

        page1Group.Items[0].Id.Should().Be(card1.Id);
        page1Group.Items[1].Id.Should().Be(card2.Id);
    }

    [Fact]
    public void Remove_ShouldRemoveCardAndReindexRemaining()
    {
        var session = new DialogueFeedSession();
        session.Load(_testBubbles, _pageMap);

        var cardToDelete = session.VisibleCards[1];
        session.Remove(cardToDelete.Id);

        session.TotalCount.Should().Be(3);
        session.VisibleCards.Should().HaveCount(3);
        session.VisibleCards[0].DisplayNumber.Should().Be(1);
        session.VisibleCards[1].DisplayNumber.Should().Be(2);
        session.VisibleCards[2].DisplayNumber.Should().Be(3);
        session.PageGroups[0].Items.Should().HaveCount(1);
    }
}
