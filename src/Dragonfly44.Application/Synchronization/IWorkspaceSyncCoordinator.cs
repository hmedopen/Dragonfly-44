namespace Dragonfly44.Application.Synchronization;

/// <summary>
/// Identifies the component or interaction that initiated a selection or navigation change.
/// Used to eliminate bidirectional synchronization loops.
/// </summary>
public enum SelectionOrigin
{
    None = 0,
    Canvas = 1,
    DialogueFeed = 2,
    KeyboardShortcut = 3,
    External = 4
}

/// <summary>
/// Event arguments for dialogue bubble / card selection changes.
/// </summary>
public sealed class SelectionChangedEventArgs : EventArgs
{
    public Guid? SelectedBubbleId { get; }
    public int? PageNumber { get; }
    public SelectionOrigin Origin { get; }
    public long Version { get; }

    public SelectionChangedEventArgs(Guid? selectedBubbleId, int? pageNumber, SelectionOrigin origin, long version)
    {
        SelectedBubbleId = selectedBubbleId;
        PageNumber = pageNumber;
        Origin = origin;
        Version = version;
    }
}

/// <summary>
/// Event arguments for active page navigation changes.
/// </summary>
public sealed class PageChangedEventArgs : EventArgs
{
    public int PageNumber { get; }
    public SelectionOrigin Origin { get; }
    public long Version { get; }

    public PageChangedEventArgs(int pageNumber, SelectionOrigin origin, long version)
    {
        PageNumber = pageNumber;
        Origin = origin;
        Version = version;
    }
}

/// <summary>
/// Event arguments for active editing state changes.
/// </summary>
public sealed class BubbleEditingChangedEventArgs : EventArgs
{
    public Guid? EditingBubbleId { get; }
    public SelectionOrigin Origin { get; }

    public BubbleEditingChangedEventArgs(Guid? editingBubbleId, SelectionOrigin origin)
    {
        EditingBubbleId = editingBubbleId;
        Origin = origin;
    }
}

/// <summary>
/// Event arguments for canvas bubble hover changes.
/// </summary>
public sealed class BubbleHoverChangedEventArgs : EventArgs
{
    public Guid? HoveredBubbleId { get; }
    public SelectionOrigin Origin { get; }

    public BubbleHoverChangedEventArgs(Guid? hoveredBubbleId, SelectionOrigin origin)
    {
        HoveredBubbleId = hoveredBubbleId;
        Origin = origin;
    }
}

/// <summary>
/// Authoritative coordinator managing synchronization between the Manga Canvas and Dialogue Feed.
/// Maintains single source of truth for selection, active page, hover state, and active editing state.
/// </summary>
public interface IWorkspaceSyncCoordinator
{
    Guid? CurrentChapterId { get; }
    Guid? SelectedBubbleId { get; }
    Guid? ActiveEditingBubbleId { get; }
    Guid? HoveredBubbleId { get; }
    int ActivePageNumber { get; }
    long CurrentVersion { get; }

    event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
    event EventHandler<PageChangedEventArgs>? PageChanged;
    event EventHandler<BubbleEditingChangedEventArgs>? BubbleEditingChanged;
    event EventHandler<BubbleHoverChangedEventArgs>? BubbleHoverChanged;
    event EventHandler? ChapterReset;

    void SetChapter(Guid? chapterId);
    bool SelectBubble(Guid? bubbleId, int? pageNumber, SelectionOrigin origin);
    bool SetActivePage(int pageNumber, SelectionOrigin origin);
    bool SetActiveEditing(Guid? bubbleId, SelectionOrigin origin);
    bool SetHoveredBubble(Guid? bubbleId, SelectionOrigin origin);
    void ClearSelection(SelectionOrigin origin);
    void Reset();
}
