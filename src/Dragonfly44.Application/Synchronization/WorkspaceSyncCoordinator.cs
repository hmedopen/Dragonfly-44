namespace Dragonfly44.Application.Synchronization;

/// <summary>
/// Thread-safe, centralized coordinator for canvas/feed synchronization.
/// Manages authoritative selection state, active page tracking, hover state, active editing state,
/// and request versioning to eliminate synchronization loops and race conditions.
/// </summary>
public class WorkspaceSyncCoordinator : IWorkspaceSyncCoordinator
{
    private Guid? _currentChapterId;
    private Guid? _selectedBubbleId;
    private Guid? _activeEditingBubbleId;
    private Guid? _hoveredBubbleId;
    private int _activePageNumber = 1;
    private long _currentVersion;

    public Guid? CurrentChapterId => _currentChapterId;
    public Guid? SelectedBubbleId => _selectedBubbleId;
    public Guid? ActiveEditingBubbleId => _activeEditingBubbleId;
    public Guid? HoveredBubbleId => _hoveredBubbleId;
    public int ActivePageNumber => _activePageNumber;
    public long CurrentVersion => Interlocked.Read(ref _currentVersion);

    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<PageChangedEventArgs>? PageChanged;
    public event EventHandler<BubbleEditingChangedEventArgs>? BubbleEditingChanged;
    public event EventHandler<BubbleHoverChangedEventArgs>? BubbleHoverChanged;
    public event EventHandler? ChapterReset;

    public void SetChapter(Guid? chapterId)
    {
        if (_currentChapterId != chapterId)
        {
            Reset();
            _currentChapterId = chapterId;
        }
    }

    public bool SelectBubble(Guid? bubbleId, int? pageNumber, SelectionOrigin origin)
    {
        if (_selectedBubbleId == bubbleId)
        {
            // If already selected, do not re-broadcast to avoid loops
            return false;
        }

        _selectedBubbleId = bubbleId;

        if (pageNumber.HasValue && pageNumber.Value > 0)
        {
            _activePageNumber = pageNumber.Value;
        }

        long version = Interlocked.Increment(ref _currentVersion);
        SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(bubbleId, pageNumber, origin, version));
        return true;
    }

    public bool SetActivePage(int pageNumber, SelectionOrigin origin)
    {
        if (pageNumber <= 0 || _activePageNumber == pageNumber)
        {
            return false;
        }

        _activePageNumber = pageNumber;
        long version = Interlocked.Increment(ref _currentVersion);
        PageChanged?.Invoke(this, new PageChangedEventArgs(pageNumber, origin, version));
        return true;
    }

    public bool SetActiveEditing(Guid? bubbleId, SelectionOrigin origin)
    {
        if (_activeEditingBubbleId == bubbleId)
        {
            return false;
        }

        _activeEditingBubbleId = bubbleId;
        BubbleEditingChanged?.Invoke(this, new BubbleEditingChangedEventArgs(bubbleId, origin));
        return true;
    }

    public bool SetHoveredBubble(Guid? bubbleId, SelectionOrigin origin)
    {
        if (_hoveredBubbleId == bubbleId)
        {
            return false;
        }

        _hoveredBubbleId = bubbleId;
        BubbleHoverChanged?.Invoke(this, new BubbleHoverChangedEventArgs(bubbleId, origin));
        return true;
    }

    public void ClearSelection(SelectionOrigin origin)
    {
        if (_selectedBubbleId == null)
        {
            return;
        }

        _selectedBubbleId = null;
        long version = Interlocked.Increment(ref _currentVersion);
        SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(null, null, origin, version));
    }

    public void Reset()
    {
        _selectedBubbleId = null;
        _activeEditingBubbleId = null;
        _hoveredBubbleId = null;
        _activePageNumber = 1;
        Interlocked.Increment(ref _currentVersion);

        ChapterReset?.Invoke(this, EventArgs.Empty);
    }
}
