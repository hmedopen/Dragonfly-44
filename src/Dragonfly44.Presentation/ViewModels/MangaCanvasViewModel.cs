using System.Collections.ObjectModel;
using System.Numerics;
using Dragonfly44.Application.Interfaces;
using Dragonfly44.Application.Rendering;
using Dragonfly44.Application.Synchronization;
using Dragonfly44.Domain.Entities;

namespace Dragonfly44.Presentation.ViewModels;

/// <summary>
/// ViewModel managing navigation, viewport transformations, and mode state for the manga canvas.
/// Integrates with IWorkspaceSyncCoordinator for bidirectional selection, page synchronization,
/// hover feedback, and active editing state.
/// </summary>
public class MangaCanvasViewModel : ViewModelBase
{
    private readonly struct WorldBounds
    {
        public float X { get; }
        public float Y { get; }
        public float Width { get; }
        public float Height { get; }

        public WorldBounds(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    private readonly IStorageService _storageService;
    private CanvasViewMode _mode = CanvasViewMode.Page;
    private int _currentPageIndex;
    private string _title = string.Empty;
    private float _viewportWidth = 1000f;
    private float _viewportHeight = 800f;

    public CanvasTransform Transform { get; } = new();
    public ContinuousLayoutCalculator LayoutCalculator { get; } = new();
    public ObservableCollection<MangaPageLayoutInfo> Pages { get; } = new();
    public List<PageOverlayItem> Overlays { get; } = new();

    public IWorkspaceSyncCoordinator? SyncCoordinator { get; private set; }

    public event EventHandler? RedrawRequested;
    public event EventHandler<Guid>? OverlayClicked;

    public CanvasViewMode Mode
    {
        get => _mode;
        set
        {
            if (SetProperty(ref _mode, value))
            {
                OnPropertyChanged(nameof(IsPageMode));
                OnPropertyChanged(nameof(IsContinuousMode));
                OnPropertyChanged(nameof(ModeDisplayText));
                UpdateLayout();
                RequestRedraw();
            }
        }
    }

    public bool IsPageMode => Mode == CanvasViewMode.Page;
    public bool IsContinuousMode => Mode == CanvasViewMode.Continuous;
    public string ModeDisplayText => IsPageMode ? "Page Mode" : "Continuous Mode";

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public int CurrentPageIndex
    {
        get => _currentPageIndex;
        private set
        {
            if (SetProperty(ref _currentPageIndex, value))
            {
                OnPropertyChanged(nameof(CurrentPageNumber));
                OnPropertyChanged(nameof(PageIndicatorText));
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(CanGoNext));
            }
        }
    }

    public int CurrentPageNumber => Pages.Count > 0 ? CurrentPageIndex + 1 : 0;
    public int TotalPages => Pages.Count;

    public string PageIndicatorText => Pages.Count > 0
        ? $"Page {CurrentPageNumber} of {TotalPages}"
        : "No Pages";

    public bool CanGoPrevious => CurrentPageIndex > 0;
    public bool CanGoNext => Pages.Count > 0 && CurrentPageIndex < Pages.Count - 1;

    public float Zoom
    {
        get => Transform.Zoom;
        set
        {
            Transform.Zoom = value;
            OnPropertyChanged(nameof(Zoom));
            OnPropertyChanged(nameof(ZoomPercentageText));
            RequestRedraw();
        }
    }

    public string ZoomPercentageText => $"{(int)Math.Round(Transform.Zoom * 100)}%";

    public MangaCanvasViewModel(IStorageService storageService, IWorkspaceSyncCoordinator? syncCoordinator = null)
    {
        _storageService = storageService;
        AttachSyncCoordinator(syncCoordinator);
    }

    public void AttachSyncCoordinator(IWorkspaceSyncCoordinator? syncCoordinator)
    {
        if (SyncCoordinator != null)
        {
            SyncCoordinator.SelectionChanged -= OnCoordinatorSelectionChanged;
            SyncCoordinator.PageChanged -= OnCoordinatorPageChanged;
            SyncCoordinator.BubbleEditingChanged -= OnCoordinatorBubbleEditingChanged;
            SyncCoordinator.BubbleHoverChanged -= OnCoordinatorBubbleHoverChanged;
            SyncCoordinator.ChapterReset -= OnCoordinatorChapterReset;
        }

        SyncCoordinator = syncCoordinator;

        if (SyncCoordinator != null)
        {
            SyncCoordinator.SelectionChanged += OnCoordinatorSelectionChanged;
            SyncCoordinator.PageChanged += OnCoordinatorPageChanged;
            SyncCoordinator.BubbleEditingChanged += OnCoordinatorBubbleEditingChanged;
            SyncCoordinator.BubbleHoverChanged += OnCoordinatorBubbleHoverChanged;
            SyncCoordinator.ChapterReset += OnCoordinatorChapterReset;
        }
    }

    private void OnCoordinatorSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.SelectedBubbleId.HasValue)
        {
            SelectOverlay(e.SelectedBubbleId.Value);

            // If selection came from outside Canvas (e.g. Dialogue Feed or Shortcut), bring bubble into view
            if (e.Origin != SelectionOrigin.Canvas)
            {
                EnsureBubbleVisible(e.SelectedBubbleId.Value, preserveZoom: true);
            }
        }
        else
        {
            ClearSelectedOverlays();
        }
    }

    private void OnCoordinatorPageChanged(object? sender, PageChangedEventArgs e)
    {
        if (e.Origin != SelectionOrigin.Canvas && e.PageNumber > 0)
        {
            SetPage(e.PageNumber - 1);
        }
    }

    private void OnCoordinatorBubbleEditingChanged(object? sender, BubbleEditingChangedEventArgs e)
    {
        SetActiveEditingOverlay(e.EditingBubbleId);
    }

    private void OnCoordinatorBubbleHoverChanged(object? sender, BubbleHoverChangedEventArgs e)
    {
        if (e.Origin != SelectionOrigin.Canvas)
        {
            SetHoveredOverlay(e.HoveredBubbleId);
        }
    }

    private void OnCoordinatorChapterReset(object? sender, EventArgs e)
    {
        ClearSelectedOverlays();
        SetHoveredOverlay(null);
        SetActiveEditingOverlay(null);
    }

    public void LoadPages(IReadOnlyList<Page> pages, string title, int initialPageIndex = 0)
    {
        Title = title;
        Pages.Clear();
        Overlays.Clear();

        foreach (var p in pages.OrderBy(p => p.PageNumber))
        {
            var absPath = _storageService.ResolveAbsolutePath(p.ImageRelativePath);
            Pages.Add(new MangaPageLayoutInfo(
                p.Id,
                p.PageNumber,
                p.OriginalFileName,
                absPath,
                p.FileFormat,
                p.Width,
                p.Height));

            if (p.Bubbles != null)
            {
                foreach (var b in p.Bubbles)
                {
                    Overlays.Add(new PageOverlayItem
                    {
                        Id = b.Id,
                        PageNumber = p.PageNumber,
                        Bounds = new NormalizedRect((float)b.X, (float)b.Y, (float)b.Width, (float)b.Height),
                        ReadingOrder = b.ReadingOrderIndex,
                        IsSelected = false,
                        IsHovered = false,
                        IsEditing = false,
                        Label = $"#{b.ReadingOrderIndex}"
                    });
                }
            }
        }

        OnPropertyChanged(nameof(TotalPages));
        CurrentPageIndex = Math.Clamp(initialPageIndex, 0, Math.Max(0, Pages.Count - 1));

        UpdateLayout();
        FitPage();
    }

    public void LoadDraftPages(IReadOnlyList<DraftPage> draftPages, string title, int initialPageIndex = 0)
    {
        Title = title;
        Pages.Clear();
        Overlays.Clear();

        foreach (var p in draftPages.OrderBy(p => p.PageNumber))
        {
            var absPath = _storageService.ResolveAbsolutePath(p.ImageRelativePath);
            Pages.Add(new MangaPageLayoutInfo(
                p.Id,
                p.PageNumber,
                p.OriginalFileName,
                absPath,
                p.FileFormat,
                p.Width,
                p.Height));
        }

        OnPropertyChanged(nameof(TotalPages));
        CurrentPageIndex = Math.Clamp(initialPageIndex, 0, Math.Max(0, Pages.Count - 1));

        UpdateLayout();
        FitPage();
    }

    public void SetViewportSize(float width, float height)
    {
        if (width <= 0 || height <= 0) return;
        var sizeChanged = Math.Abs(_viewportWidth - width) > 1f || Math.Abs(_viewportHeight - height) > 1f;
        _viewportWidth = width;
        _viewportHeight = height;

        if (sizeChanged && Pages.Count > 0)
        {
            FitPage();
        }
    }

    private void UpdateLayout()
    {
        if (Pages.Count == 0) return;

        if (IsContinuousMode)
        {
            LayoutCalculator.LayoutPages(Pages);
        }
    }

    public void NextPage()
    {
        if (CanGoNext)
        {
            SetPage(CurrentPageIndex + 1);
        }
    }

    public void PreviousPage()
    {
        if (CanGoPrevious)
        {
            SetPage(CurrentPageIndex - 1);
        }
    }

    public void SetPage(int pageIndex)
    {
        if (Pages.Count == 0) return;
        var clamped = Math.Clamp(pageIndex, 0, Pages.Count - 1);
        CurrentPageIndex = clamped;

        SyncCoordinator?.SetActivePage(CurrentPageNumber, SelectionOrigin.Canvas);

        if (IsPageMode)
        {
            FitPage();
        }
        else
        {
            // Scroll to target page in continuous mode
            var targetY = LayoutCalculator.GetPageOffset(Pages, clamped);
            Transform.Pan = new Vector2(Transform.Pan.X, -targetY * Transform.Zoom + 40f);
            RequestRedraw();
        }
    }

    public void ZoomIn(float step = 0.15f)
    {
        var center = new Vector2(_viewportWidth * 0.5f, _viewportHeight * 0.5f);
        Transform.ZoomAt(center, Transform.Zoom + step);
        NotifyZoomChanged();
    }

    public void ZoomOut(float step = 0.15f)
    {
        var center = new Vector2(_viewportWidth * 0.5f, _viewportHeight * 0.5f);
        Transform.ZoomAt(center, Transform.Zoom - step);
        NotifyZoomChanged();
    }

    public void ZoomAtCursor(Vector2 cursorPoint, float delta)
    {
        var newZoom = Transform.Zoom * (delta > 0 ? 1.12f : 0.89f);
        Transform.ZoomAt(cursorPoint, newZoom);
        NotifyZoomChanged();
    }

    public void PanBy(Vector2 delta)
    {
        Transform.Pan += delta;
        RequestRedraw();
    }

    public void FitPage()
    {
        if (Pages.Count == 0 || _viewportWidth <= 0 || _viewportHeight <= 0) return;

        var activePage = Pages[CurrentPageIndex];
        var (fitZoom, fitPan) = CanvasTransform.CalculateFitPage(
            _viewportWidth,
            _viewportHeight,
            activePage.Width,
            activePage.Height);

        Transform.Zoom = fitZoom;
        Transform.Pan = fitPan;
        NotifyZoomChanged();
    }

    public void FitWidth()
    {
        if (Pages.Count == 0 || _viewportWidth <= 0 || _viewportHeight <= 0) return;

        var activePage = Pages[CurrentPageIndex];
        var (fitZoom, fitPan) = CanvasTransform.CalculateFitWidth(
            _viewportWidth,
            _viewportHeight,
            activePage.Width,
            activePage.Height);

        Transform.Zoom = fitZoom;
        Transform.Pan = fitPan;
        NotifyZoomChanged();
    }

    public void ResetZoom()
    {
        if (Pages.Count == 0) return;
        var activePage = Pages[CurrentPageIndex];
        Transform.Reset(_viewportWidth, _viewportHeight, activePage.Width, activePage.Height);
        NotifyZoomChanged();
    }

    public void ToggleViewMode()
    {
        var targetPage = CurrentPageIndex;
        Mode = IsPageMode ? CanvasViewMode.Continuous : CanvasViewMode.Page;
        SetPage(targetPage);
    }

    public void UpdateContinuousScrollPosition(float panDeltaY)
    {
        if (!IsContinuousMode || Pages.Count == 0) return;

        Transform.Pan = new Vector2(Transform.Pan.X, Transform.Pan.Y + panDeltaY);

        var worldCenter = Transform.ScreenToWorld(new Vector2(_viewportWidth * 0.5f, _viewportHeight * 0.5f));
        var newIndex = LayoutCalculator.GetPrimaryPageIndexAt(Pages, worldCenter.Y);
        if (newIndex != CurrentPageIndex)
        {
            CurrentPageIndex = newIndex;
            SyncCoordinator?.SetActivePage(CurrentPageNumber, SelectionOrigin.Canvas);
        }

        RequestRedraw();
    }

    private void NotifyZoomChanged()
    {
        OnPropertyChanged(nameof(Zoom));
        OnPropertyChanged(nameof(ZoomPercentageText));
        RequestRedraw();
    }

    // -------------------------------------------------------------
    // VIEWPORT NAVIGATION & VISIBILITY (REQUIREMENT D)
    // -------------------------------------------------------------

    public bool EnsureBubbleVisible(Guid bubbleId, bool preserveZoom = true)
    {
        if (Pages.Count == 0 || Overlays.Count == 0) return false;

        var overlay = Overlays.FirstOrDefault(o => o.Id == bubbleId);
        if (overlay == null) return false;

        var targetPageIndex = -1;
        for (int i = 0; i < Pages.Count; i++)
        {
            if (Pages[i].PageNumber == overlay.PageNumber)
            {
                targetPageIndex = i;
                break;
            }
        }

        if (targetPageIndex < 0) return false;

        var page = Pages[targetPageIndex];

        if (IsPageMode)
        {
            bool pageChanged = CurrentPageIndex != targetPageIndex;
            if (pageChanged)
            {
                CurrentPageIndex = targetPageIndex;
                SyncCoordinator?.SetActivePage(CurrentPageNumber, SelectionOrigin.Canvas);
            }

            var (px, py, pw, ph) = overlay.Bounds.ToPixels(page.Width, page.Height);
            var bubbleRect = new WorldBounds(px, py, pw, ph);

            // If already visible on screen, avoid unnecessary camera movement!
            if (!pageChanged && IsRectVisibleInViewport(bubbleRect, margin: 40f))
            {
                RequestRedraw();
                return true;
            }

            CenterOnWorldRect(bubbleRect, preserveZoom, page.Width, page.Height);
            RequestRedraw();
            return true;
        }
        else
        {
            // Continuous strip mode
            var (px, py, pw, ph) = overlay.Bounds.ToPixels(page.Width, page.Height);
            var worldRect = new WorldBounds(px, page.OffsetY + py, pw, ph);

            if (IsRectVisibleInViewport(worldRect, margin: 40f))
            {
                CurrentPageIndex = targetPageIndex;
                RequestRedraw();
                return true;
            }

            CenterOnWorldRect(worldRect, preserveZoom, page.Width, page.Height);
            CurrentPageIndex = targetPageIndex;
            SyncCoordinator?.SetActivePage(CurrentPageNumber, SelectionOrigin.Canvas);
            RequestRedraw();
            return true;
        }
    }

    private bool IsRectVisibleInViewport(WorldBounds rect, float margin = 40f)
    {
        if (_viewportWidth <= 0 || _viewportHeight <= 0) return false;

        var screenTopLeft = Transform.WorldToScreen(new Vector2(rect.X, rect.Y));
        var screenBottomRight = Transform.WorldToScreen(new Vector2(rect.X + rect.Width, rect.Y + rect.Height));

        var minX = Math.Min(screenTopLeft.X, screenBottomRight.X);
        var maxX = Math.Max(screenTopLeft.X, screenBottomRight.X);
        var minY = Math.Min(screenTopLeft.Y, screenBottomRight.Y);
        var maxY = Math.Max(screenTopLeft.Y, screenBottomRight.Y);

        return minX >= margin &&
               maxX <= (_viewportWidth - margin) &&
               minY >= margin &&
               maxY <= (_viewportHeight - margin);
    }

    private void CenterOnWorldRect(WorldBounds rect, bool preserveZoom, float pageWidth, float pageHeight)
    {
        float targetZoom = Transform.Zoom;
        if (!preserveZoom || targetZoom <= 0.1f)
        {
            var (fitZoom, _) = CanvasTransform.CalculateFitPage(_viewportWidth, _viewportHeight, pageWidth, pageHeight);
            targetZoom = fitZoom;
            Transform.Zoom = targetZoom;
        }

        var bubbleCenter = new Vector2(
            rect.X + (rect.Width * 0.5f),
            rect.Y + (rect.Height * 0.5f));

        var targetPan = new Vector2(_viewportWidth * 0.5f, _viewportHeight * 0.5f) - (bubbleCenter * targetZoom);

        if (IsContinuousMode && (pageWidth * targetZoom) <= _viewportWidth)
        {
            targetPan.X = (_viewportWidth - (pageWidth * targetZoom)) * 0.5f;
        }

        Transform.Pan = targetPan;
        NotifyZoomChanged();
    }

    // -------------------------------------------------------------
    // OVERLAY STATE & DYNAMIC DATA INTEGRITY
    // -------------------------------------------------------------

    public void SelectOverlay(Guid bubbleId)
    {
        bool changed = false;
        foreach (var item in Overlays)
        {
            var shouldSelect = item.Id == bubbleId;
            if (item.IsSelected != shouldSelect)
            {
                item.IsSelected = shouldSelect;
                changed = true;
            }
        }
        if (changed)
        {
            RequestRedraw();
        }
    }

    public void ClearSelectedOverlays()
    {
        bool changed = false;
        foreach (var item in Overlays)
        {
            if (item.IsSelected)
            {
                item.IsSelected = false;
                changed = true;
            }
        }
        if (changed)
        {
            RequestRedraw();
        }
    }

    public bool SetHoveredOverlay(Guid? bubbleId)
    {
        bool changed = false;
        foreach (var item in Overlays)
        {
            var shouldHover = bubbleId.HasValue && item.Id == bubbleId.Value;
            if (item.IsHovered != shouldHover)
            {
                item.IsHovered = shouldHover;
                changed = true;
            }
        }
        if (changed)
        {
            RequestRedraw();
        }
        return changed;
    }

    public bool SetActiveEditingOverlay(Guid? bubbleId)
    {
        bool changed = false;
        foreach (var item in Overlays)
        {
            var shouldEdit = bubbleId.HasValue && item.Id == bubbleId.Value;
            if (item.IsEditing != shouldEdit)
            {
                item.IsEditing = shouldEdit;
                changed = true;
            }
        }
        if (changed)
        {
            RequestRedraw();
        }
        return changed;
    }

    public void AddOverlay(Bubble bubble, int pageNumber)
    {
        var existing = Overlays.FirstOrDefault(o => o.Id == bubble.Id);
        if (existing != null) return;

        Overlays.Add(new PageOverlayItem
        {
            Id = bubble.Id,
            PageNumber = pageNumber,
            Bounds = new NormalizedRect((float)bubble.X, (float)bubble.Y, (float)bubble.Width, (float)bubble.Height),
            ReadingOrder = bubble.ReadingOrderIndex,
            IsSelected = false,
            IsHovered = false,
            IsEditing = false,
            Label = $"#{bubble.ReadingOrderIndex}"
        });

        RequestRedraw();
    }

    public void RemoveOverlay(Guid bubbleId)
    {
        var existing = Overlays.FirstOrDefault(o => o.Id == bubbleId);
        if (existing != null)
        {
            Overlays.Remove(existing);
            RequestRedraw();
        }
    }

    public void UpdateOverlaysReadingOrder(Guid pageId, IReadOnlyList<Guid> orderedBubbleIds)
    {
        bool changed = false;
        for (int i = 0; i < orderedBubbleIds.Count; i++)
        {
            var id = orderedBubbleIds[i];
            var overlay = Overlays.FirstOrDefault(o => o.Id == id);
            if (overlay != null)
            {
                int newOrder = i + 1;
                if (overlay.ReadingOrder != newOrder)
                {
                    overlay.ReadingOrder = newOrder;
                    overlay.Label = $"#{newOrder}";
                    changed = true;
                }
            }
        }

        if (changed)
        {
            RequestRedraw();
        }
    }

    public void NotifyOverlayClicked(Guid bubbleId)
    {
        var overlay = Overlays.FirstOrDefault(o => o.Id == bubbleId);
        if (SyncCoordinator != null)
        {
            SyncCoordinator.SelectBubble(bubbleId, overlay?.PageNumber, SelectionOrigin.Canvas);
        }
        else
        {
            SelectOverlay(bubbleId);
            OverlayClicked?.Invoke(this, bubbleId);
        }
    }

    public void NotifyBackgroundClicked()
    {
        if (SyncCoordinator != null)
        {
            SyncCoordinator.ClearSelection(SelectionOrigin.Canvas);
        }
        else
        {
            ClearSelectedOverlays();
        }
    }

    public void NotifyBubbleHovered(Guid? bubbleId)
    {
        if (SyncCoordinator != null)
        {
            SyncCoordinator.SetHoveredBubble(bubbleId, SelectionOrigin.Canvas);
        }
        SetHoveredOverlay(bubbleId);
    }

    public void Clear()
    {
        Pages.Clear();
        Overlays.Clear();
        Title = string.Empty;
        CurrentPageIndex = 0;
        Transform.Zoom = 1.0f;
        Transform.Pan = Vector2.Zero;
        RequestRedraw();
    }

    public void RequestRedraw()
    {
        RedrawRequested?.Invoke(this, EventArgs.Empty);
    }
}
