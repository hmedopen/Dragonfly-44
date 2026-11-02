using System.Numerics;

namespace Dragonfly44.Application.Rendering;

/// <summary>
/// Core reading session model managing page navigation, layout calculations,
/// zoom/pan transformations, and viewing modes independently of UI frameworks.
/// </summary>
public class CanvasNavigationSession
{
    private CanvasViewMode _mode = CanvasViewMode.Page;
    private int _currentPageIndex;
    private readonly List<MangaPageLayoutInfo> _pages = new();

    public string Title { get; set; } = string.Empty;
    public CanvasTransform Transform { get; } = new();
    public ContinuousLayoutCalculator LayoutCalculator { get; } = new();
    public IReadOnlyList<MangaPageLayoutInfo> Pages => _pages;
    public List<PageOverlayItem> Overlays { get; } = new();

    public CanvasViewMode Mode
    {
        get => _mode;
        set
        {
            _mode = value;
            UpdateLayout();
        }
    }

    public bool IsPageMode => Mode == CanvasViewMode.Page;
    public bool IsContinuousMode => Mode == CanvasViewMode.Continuous;
    public string ModeDisplayText => IsPageMode ? "Page Mode" : "Continuous Mode";

    public int CurrentPageIndex
    {
        get => _currentPageIndex;
        private set => _currentPageIndex = value;
    }

    public int CurrentPageNumber => _pages.Count > 0 ? _currentPageIndex + 1 : 0;
    public int TotalPages => _pages.Count;

    public string PageIndicatorText => _pages.Count > 0
        ? $"Page {CurrentPageNumber} of {TotalPages}"
        : "No Pages";

    public bool CanGoPrevious => _currentPageIndex > 0;
    public bool CanGoNext => _pages.Count > 0 && _currentPageIndex < _pages.Count - 1;

    public void LoadPages(IEnumerable<MangaPageLayoutInfo> pages, string title, int initialPageIndex = 0)
    {
        Title = title;
        _pages.Clear();
        _pages.AddRange(pages.OrderBy(p => p.PageNumber));

        _currentPageIndex = Math.Clamp(initialPageIndex, 0, Math.Max(0, _pages.Count - 1));
        UpdateLayout();
    }

    public void NextPage()
    {
        if (CanGoNext)
        {
            SetPage(_currentPageIndex + 1);
        }
    }

    public void PreviousPage()
    {
        if (CanGoPrevious)
        {
            SetPage(_currentPageIndex - 1);
        }
    }

    public void SetPage(int pageIndex)
    {
        if (_pages.Count == 0) return;
        _currentPageIndex = Math.Clamp(pageIndex, 0, _pages.Count - 1);
    }

    public void ToggleViewMode()
    {
        var targetPage = _currentPageIndex;
        Mode = IsPageMode ? CanvasViewMode.Continuous : CanvasViewMode.Page;
        SetPage(targetPage);
    }

    public void UpdateLayout()
    {
        if (_pages.Count == 0) return;

        if (IsContinuousMode)
        {
            LayoutCalculator.LayoutPages(_pages);
        }
    }

    public void ZoomIn(float step = 0.15f, float vpWidth = 1000f, float vpHeight = 800f)
    {
        var center = new Vector2(vpWidth * 0.5f, vpHeight * 0.5f);
        Transform.ZoomAt(center, Transform.Zoom + step);
    }

    public void ZoomOut(float step = 0.15f, float vpWidth = 1000f, float vpHeight = 800f)
    {
        var center = new Vector2(vpWidth * 0.5f, vpHeight * 0.5f);
        Transform.ZoomAt(center, Transform.Zoom - step);
    }

    public void FitPage(float vpWidth, float vpHeight)
    {
        if (_pages.Count == 0 || vpWidth <= 0 || vpHeight <= 0) return;
        var page = _pages[_currentPageIndex];
        var (fitZoom, fitPan) = CanvasTransform.CalculateFitPage(vpWidth, vpHeight, page.Width, page.Height);
        Transform.Zoom = fitZoom;
        Transform.Pan = fitPan;
    }

    public void FitWidth(float vpWidth, float vpHeight)
    {
        if (_pages.Count == 0 || vpWidth <= 0 || vpHeight <= 0) return;
        var page = _pages[_currentPageIndex];
        var (fitZoom, fitPan) = CanvasTransform.CalculateFitWidth(vpWidth, vpHeight, page.Width, page.Height);
        Transform.Zoom = fitZoom;
        Transform.Pan = fitPan;
    }

    public void ResetZoom(float vpWidth, float vpHeight)
    {
        if (_pages.Count == 0) return;
        var page = _pages[_currentPageIndex];
        Transform.Reset(vpWidth, vpHeight, page.Width, page.Height);
    }

    public bool EnsureBubbleVisible(Guid bubbleId, float vpWidth, float vpHeight, bool preserveZoom = true)
    {
        var overlay = Overlays.FirstOrDefault(o => o.Id == bubbleId);
        if (overlay == null || _pages.Count == 0 || vpWidth <= 0 || vpHeight <= 0) return false;

        var targetIndex = _pages.FindIndex(p => p.PageNumber == overlay.PageNumber);
        if (targetIndex < 0) return false;

        var page = _pages[targetIndex];

        if (IsPageMode)
        {
            bool pageChanged = _currentPageIndex != targetIndex;
            if (pageChanged)
            {
                _currentPageIndex = targetIndex;
            }

            var (px, py, pw, ph) = overlay.Bounds.ToPixels(page.Width, page.Height);
            if (!pageChanged && IsRectVisible(px, py, pw, ph, vpWidth, vpHeight, margin: 40f))
            {
                return true;
            }

            CenterOnRect(px, py, pw, ph, page.Width, page.Height, vpWidth, vpHeight, preserveZoom);
            return true;
        }
        else
        {
            var (px, py, pw, ph) = overlay.Bounds.ToPixels(page.Width, page.Height);
            var worldY = page.OffsetY + py;

            if (IsRectVisible(px, worldY, pw, ph, vpWidth, vpHeight, margin: 40f))
            {
                _currentPageIndex = targetIndex;
                return true;
            }

            CenterOnRect(px, worldY, pw, ph, page.Width, page.Height, vpWidth, vpHeight, preserveZoom);
            _currentPageIndex = targetIndex;
            return true;
        }
    }

    private bool IsRectVisible(float x, float y, float w, float h, float vpWidth, float vpHeight, float margin)
    {
        var screenTopLeft = Transform.WorldToScreen(new Vector2(x, y));
        var screenBottomRight = Transform.WorldToScreen(new Vector2(x + w, y + h));

        var minX = Math.Min(screenTopLeft.X, screenBottomRight.X);
        var maxX = Math.Max(screenTopLeft.X, screenBottomRight.X);
        var minY = Math.Min(screenTopLeft.Y, screenBottomRight.Y);
        var maxY = Math.Max(screenTopLeft.Y, screenBottomRight.Y);

        return minX >= margin && maxX <= (vpWidth - margin) &&
               minY >= margin && maxY <= (vpHeight - margin);
    }

    private void CenterOnRect(float x, float y, float w, float h, float pageWidth, float pageHeight, float vpWidth, float vpHeight, bool preserveZoom)
    {
        float targetZoom = Transform.Zoom;
        if (!preserveZoom || targetZoom <= 0.1f)
        {
            var (fitZoom, _) = CanvasTransform.CalculateFitPage(vpWidth, vpHeight, pageWidth, pageHeight);
            targetZoom = fitZoom;
            Transform.Zoom = targetZoom;
        }

        var bubbleCenter = new Vector2(x + (w * 0.5f), y + (h * 0.5f));
        var targetPan = new Vector2(vpWidth * 0.5f, vpHeight * 0.5f) - (bubbleCenter * targetZoom);

        if (IsContinuousMode && (pageWidth * targetZoom) <= vpWidth)
        {
            targetPan.X = (vpWidth - (pageWidth * targetZoom)) * 0.5f;
        }

        Transform.Pan = targetPan;
    }

    public void SelectOverlay(Guid bubbleId)
    {
        foreach (var item in Overlays)
        {
            item.IsSelected = item.Id == bubbleId;
        }
    }

    public void ClearSelectedOverlays()
    {
        foreach (var item in Overlays)
        {
            item.IsSelected = false;
        }
    }

    public void SetHoveredOverlay(Guid? bubbleId)
    {
        foreach (var item in Overlays)
        {
            item.IsHovered = bubbleId.HasValue && item.Id == bubbleId.Value;
        }
    }

    public void SetActiveEditingOverlay(Guid? bubbleId)
    {
        foreach (var item in Overlays)
        {
            item.IsEditing = bubbleId.HasValue && item.Id == bubbleId.Value;
        }
    }

    public void AddOverlay(PageOverlayItem overlay)
    {
        if (!Overlays.Any(o => o.Id == overlay.Id))
        {
            Overlays.Add(overlay);
        }
    }

    public void RemoveOverlay(Guid bubbleId)
    {
        Overlays.RemoveAll(o => o.Id == bubbleId);
    }

    public void UpdateOverlaysReadingOrder(Guid pageId, IReadOnlyList<Guid> orderedBubbleIds)
    {
        for (int i = 0; i < orderedBubbleIds.Count; i++)
        {
            var id = orderedBubbleIds[i];
            var overlay = Overlays.FirstOrDefault(o => o.Id == id);
            if (overlay != null)
            {
                int newOrder = i + 1;
                overlay.ReadingOrder = newOrder;
                overlay.Label = $"#{newOrder}";
            }
        }
    }
}
