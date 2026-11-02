using Dragonfly44.Domain.Entities;
using Dragonfly44.Domain.Enums;

namespace Dragonfly44.Application.Dialogue;

public enum DialogueFeedFilter
{
    All = 0,
    Flagged = 1
}

public class DialogueFeedGroup
{
    public Guid PageId { get; }
    public int PageNumber { get; }
    public List<DialogueFeedItem> Items { get; } = new();

    public DialogueFeedGroup(Guid pageId, int pageNumber)
    {
        PageId = pageId;
        PageNumber = pageNumber;
    }
}

public class DialogueFeedItem
{
    public Guid Id { get; set; }
    public Guid PageId { get; set; }
    public int PageNumber { get; set; }
    public int DisplayNumber { get; set; }
    public string RawText { get; set; } = string.Empty;
    public string TranslatedText { get; set; } = string.Empty;
    public BubbleShapeType ShapeType { get; set; } = BubbleShapeType.StandardSpeech;
    public int ReadingOrderIndex { get; set; }
    public bool IsFlagged { get; set; }
    public bool IsSelected { get; set; }
    public bool IsEditing { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    public string DisplayNumberText => $"#{DisplayNumber}";
    public string PageBadgeText => $"P{PageNumber}";

    public DialogueFeedItem(Bubble bubble, int pageNumber, int displayNumber)
    {
        Id = bubble.Id;
        PageId = bubble.PageId;
        PageNumber = pageNumber;
        DisplayNumber = displayNumber;
        RawText = bubble.RawText;
        TranslatedText = bubble.TranslatedText;
        ShapeType = bubble.ShapeType;
        ReadingOrderIndex = bubble.ReadingOrderIndex;
        IsFlagged = bubble.IsFlagged;
        X = bubble.X;
        Y = bubble.Y;
        Width = bubble.Width;
        Height = bubble.Height;
    }
}

/// <summary>
/// Domain-level session coordinating dialogue cards, filtering, selection, keyboard navigation,
/// reordering within page boundaries, and canvas synchronization.
/// </summary>
public class DialogueFeedSession
{
    private readonly List<DialogueFeedItem> _allCards = new();
    private readonly List<DialogueFeedGroup> _pageGroups = new();
    private DialogueFeedFilter _filter = DialogueFeedFilter.All;
    private DialogueFeedItem? _selectedItem;
    private DialogueFeedItem? _activeEditingItem;

    public IReadOnlyList<DialogueFeedItem> AllCards => _allCards;
    public IReadOnlyList<DialogueFeedGroup> PageGroups => _pageGroups;
    public IReadOnlyList<DialogueFeedItem> VisibleCards => GetVisibleCardsInternal();

    public DialogueFeedFilter Filter
    {
        get => _filter;
        set
        {
            if (_filter != value)
            {
                _filter = value;
                RefreshDisplayNumbers();
            }
        }
    }

    public DialogueFeedItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (_selectedItem != value)
            {
                if (_selectedItem != null) _selectedItem.IsSelected = false;
                _selectedItem = value;
                if (_selectedItem != null) _selectedItem.IsSelected = true;

                if (_selectedItem != null)
                {
                    ItemSelected?.Invoke(this, _selectedItem);
                }
            }
        }
    }

    public DialogueFeedItem? ActiveEditingItem
    {
        get => _activeEditingItem;
        set
        {
            if (_activeEditingItem != value)
            {
                if (_activeEditingItem != null) _activeEditingItem.IsEditing = false;
                _activeEditingItem = value;
                if (_activeEditingItem != null)
                {
                    _activeEditingItem.IsEditing = true;
                    SelectedItem = _activeEditingItem;
                }
            }
        }
    }

    public int TotalCount => _allCards.Count;
    public int FlaggedCount => _allCards.Count(c => c.IsFlagged);

    public event EventHandler<DialogueFeedItem>? ItemSelected;
    public event EventHandler<int>? NavigateToPageRequested;

    public void Load(IEnumerable<Bubble> bubbles, IDictionary<Guid, int> pageNumberMap)
    {
        _allCards.Clear();
        _pageGroups.Clear();
        SelectedItem = null;
        ActiveEditingItem = null;

        var bubblesList = bubbles.ToList();
        var groupedByPage = bubblesList
            .GroupBy(b => b.PageId)
            .OrderBy(g => pageNumberMap.TryGetValue(g.Key, out var pn) ? pn : int.MaxValue);

        int dispNum = 1;
        foreach (var group in groupedByPage)
        {
            var pageId = group.Key;
            var pageNum = pageNumberMap.TryGetValue(pageId, out var pn) ? pn : 1;
            var feedGroup = new DialogueFeedGroup(pageId, pageNum);

            foreach (var bubble in group.OrderBy(b => b.ReadingOrderIndex))
            {
                var item = new DialogueFeedItem(bubble, pageNum, dispNum++);
                feedGroup.Items.Add(item);
                _allCards.Add(item);
            }

            _pageGroups.Add(feedGroup);
        }

        RefreshDisplayNumbers();
    }

    public void SelectById(Guid bubbleId)
    {
        var item = _allCards.FirstOrDefault(c => c.Id == bubbleId);
        if (item != null)
        {
            SelectedItem = item;
            NavigateToPageRequested?.Invoke(this, item.PageNumber);
        }
    }

    public void SelectNext()
    {
        var visible = VisibleCards;
        if (visible.Count == 0) return;

        if (SelectedItem == null)
        {
            SelectedItem = visible[0];
            return;
        }

        var idx = visible.ToList().IndexOf(SelectedItem);
        if (idx >= 0 && idx < visible.Count - 1)
        {
            SelectedItem = visible[idx + 1];
        }
    }

    public void SelectPrevious()
    {
        var visible = VisibleCards;
        if (visible.Count == 0) return;

        if (SelectedItem == null)
        {
            SelectedItem = visible[visible.Count - 1];
            return;
        }

        var idx = visible.ToList().IndexOf(SelectedItem);
        if (idx > 0)
        {
            SelectedItem = visible[idx - 1];
        }
    }

    public void StartEditingSelected()
    {
        if (SelectedItem != null)
        {
            ActiveEditingItem = SelectedItem;
        }
    }

    public void FinishEditing()
    {
        ActiveEditingItem = null;
    }

    public void ToggleFlag(Guid bubbleId)
    {
        var item = _allCards.FirstOrDefault(c => c.Id == bubbleId);
        if (item != null)
        {
            item.IsFlagged = !item.IsFlagged;
            RefreshDisplayNumbers();
        }
    }

    public bool MoveUp(Guid bubbleId)
    {
        var item = _allCards.FirstOrDefault(c => c.Id == bubbleId);
        if (item == null) return false;

        var group = _pageGroups.FirstOrDefault(g => g.PageId == item.PageId);
        if (group == null) return false;

        var idx = group.Items.IndexOf(item);
        if (idx > 0)
        {
            group.Items.RemoveAt(idx);
            group.Items.Insert(idx - 1, item);
            RebuildAllCardsFromGroups();
            return true;
        }

        return false;
    }

    public bool MoveDown(Guid bubbleId)
    {
        var item = _allCards.FirstOrDefault(c => c.Id == bubbleId);
        if (item == null) return false;

        var group = _pageGroups.FirstOrDefault(g => g.PageId == item.PageId);
        if (group == null) return false;

        var idx = group.Items.IndexOf(item);
        if (idx >= 0 && idx < group.Items.Count - 1)
        {
            group.Items.RemoveAt(idx);
            group.Items.Insert(idx + 1, item);
            RebuildAllCardsFromGroups();
            return true;
        }

        return false;
    }

    public void Remove(Guid bubbleId)
    {
        var item = _allCards.FirstOrDefault(c => c.Id == bubbleId);
        if (item == null) return;

        if (SelectedItem == item) SelectedItem = null;
        if (ActiveEditingItem == item) ActiveEditingItem = null;

        _allCards.Remove(item);
        var group = _pageGroups.FirstOrDefault(g => g.PageId == item.PageId);
        group?.Items.Remove(item);

        RefreshDisplayNumbers();
    }

    private void RebuildAllCardsFromGroups()
    {
        _allCards.Clear();
        foreach (var group in _pageGroups)
        {
            _allCards.AddRange(group.Items);
        }
        RefreshDisplayNumbers();
    }

    private void RefreshDisplayNumbers()
    {
        var visible = GetVisibleCardsInternal();
        int num = 1;
        foreach (var card in visible)
        {
            card.DisplayNumber = num++;
        }
    }

    private IReadOnlyList<DialogueFeedItem> GetVisibleCardsInternal()
    {
        return Filter switch
        {
            DialogueFeedFilter.Flagged => _allCards.Where(c => c.IsFlagged).ToList(),
            _ => _allCards.ToList()
        };
    }
}
