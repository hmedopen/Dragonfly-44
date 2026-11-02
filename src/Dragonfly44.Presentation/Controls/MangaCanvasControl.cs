using System.Numerics;
using Dragonfly44.Application.Rendering;
using Dragonfly44.Presentation.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Dragonfly44.Presentation.Rendering;

/// <summary>
/// Hardware-accelerated WinUI 3 control hosting the manga rendering canvas,
/// mouse pan/zoom interactions, touch gestures, and keyboard navigation.
/// </summary>
public class MangaCanvasControl : ContentControl, IDisposable
{
    private CanvasControl? _canvas;
    private MangaCanvasRenderer? _renderer;
    private MangaImageCache? _imageCache;
    private IMangaImageLoader? _imageLoader;
    private MangaCanvasViewModel? _viewModel;

    private bool _isPanning;
    private Vector2 _lastPointerPos;
    private Vector2 _pointerPressPos;
    private Guid? _currentHoveredId;
    private bool _isDisposed;

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(MangaCanvasViewModel),
            typeof(MangaCanvasControl),
            new PropertyMetadata(null, OnViewModelChangedStatic));

    public MangaCanvasViewModel? ViewModel
    {
        get => (MangaCanvasViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MangaCanvasControl control)
        {
            if (e.OldValue is MangaCanvasViewModel oldVm)
            {
                oldVm.RedrawRequested -= control.OnRedrawRequested;
            }

            control._viewModel = e.NewValue as MangaCanvasViewModel;

            if (control._viewModel != null)
            {
                control._viewModel.RedrawRequested += control.OnRedrawRequested;
                control._canvas?.Invalidate();
            }
        }
    }

    public MangaCanvasControl()
    {
        IsTabStop = true;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;

        // Interaction handlers
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += OnPointerCaptureLost;
        PointerExited += OnPointerExited;
        PointerWheelChanged += OnPointerWheelChanged;
        DoubleTapped += OnDoubleTapped;
        KeyDown += OnKeyDown;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_canvas == null)
        {
            _imageLoader = new MangaImageLoader(NullLogger<MangaImageLoader>.Instance);
            _imageCache = new MangaImageCache(_imageLoader, NullLogger<MangaImageCache>.Instance, maxCapacity: 8);
            _renderer = new MangaCanvasRenderer();

            _canvas = new CanvasControl();
            _canvas.CreateResources += OnCanvasCreateResources;
            _canvas.Draw += OnCanvasDraw;
            Content = _canvas;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DisposeGpuResources();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_viewModel != null && e.NewSize.Width > 0 && e.NewSize.Height > 0)
        {
            _viewModel.SetViewportSize((float)e.NewSize.Width, (float)e.NewSize.Height);
        }
    }

    private void OnCanvasCreateResources(CanvasControl sender, Microsoft.Graphics.Canvas.UI.CanvasCreateResourcesEventArgs args)
    {
        _imageCache?.Clear();
    }

    private void OnCanvasDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        if (_viewModel == null || _renderer == null || _imageCache == null) return;

        var width = (float)sender.ActualWidth;
        var height = (float)sender.ActualHeight;

        if (width <= 0 || height <= 0) return;

        _viewModel.SetViewportSize(width, height);

        // Preload visible pages asynchronously without blocking drawing
        PreloadVisiblePages(sender);

        _renderer.Draw(
            args.DrawingSession,
            width,
            height,
            _viewModel.Mode,
            _viewModel.Transform,
            _viewModel.Pages,
            _viewModel.CurrentPageIndex,
            relPath => _imageCache.TryGet(relPath),
            _viewModel.Overlays);
    }

    private void PreloadVisiblePages(CanvasControl sender)
    {
        if (_viewModel == null || _imageCache == null || _viewModel.Pages.Count == 0) return;

        IEnumerable<int> pagesToLoad;

        if (_viewModel.IsPageMode)
        {
            var cur = _viewModel.CurrentPageIndex;
            var list = new List<int> { cur };
            // Pre-fetch adjacent pages (prev and next)
            if (cur + 1 < _viewModel.Pages.Count) list.Add(cur + 1);
            if (cur - 1 >= 0) list.Add(cur - 1);
            pagesToLoad = list;
        }
        else
        {
            // Continuous mode: query visible pages + buffer
            var worldTopLeft = _viewModel.Transform.ScreenToWorld(Vector2.Zero);
            var worldBottomRight = _viewModel.Transform.ScreenToWorld(new Vector2((float)sender.ActualWidth, (float)sender.ActualHeight));
            var visibleIndices = _viewModel.LayoutCalculator.GetVisiblePageIndices(
                _viewModel.Pages,
                Math.Min(worldTopLeft.Y, worldBottomRight.Y),
                Math.Max(worldTopLeft.Y, worldBottomRight.Y));
            pagesToLoad = visibleIndices;
        }

        foreach (var idx in pagesToLoad)
        {
            if (idx >= 0 && idx < _viewModel.Pages.Count)
            {
                var page = _viewModel.Pages[idx];
                var key = page.ImageRelativePath;
                if (_imageCache.TryGet(key) == null)
                {
                    // Asynchronously load and invalidate on completion
                    _ = _imageCache.GetOrLoadAsync(sender, key, key).ContinueWith(t =>
                    {
                        if (t.IsCompletedSuccessfully && t.Result != null)
                        {
                            DispatcherQueue?.TryEnqueue(() => _canvas?.Invalidate());
                        }
                    });
                }
            }
        }
    }

    private void OnRedrawRequested(object? sender, EventArgs e)
    {
        _canvas?.Invalidate();
    }

    // -------------------------------------------------------------
    // POINTER INTERACTIONS (PAN, ZOOM)
    // -------------------------------------------------------------

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        Focus(FocusState.Programmatic);
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed || point.Properties.IsMiddleButtonPressed)
        {
            _isPanning = true;
            _lastPointerPos = new Vector2((float)point.Position.X, (float)point.Position.Y);
            _pointerPressPos = _lastPointerPos;
            CapturePointer(e.Pointer);
            e.Handled = true;
        }
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        var currentPos = new Vector2((float)point.Position.X, (float)point.Position.Y);

        if (_isPanning && _viewModel != null)
        {
            var delta = currentPos - _lastPointerPos;

            if (_viewModel.IsContinuousMode)
            {
                _viewModel.UpdateContinuousScrollPosition(delta.Y);
                _viewModel.Transform.Pan = new Vector2(_viewModel.Transform.Pan.X + delta.X, _viewModel.Transform.Pan.Y);
                _viewModel.RequestRedraw();
            }
            else
            {
                _viewModel.PanBy(delta);
            }

            _lastPointerPos = currentPos;
            e.Handled = true;
        }
        else if (!_isPanning && _viewModel != null)
        {
            // Hover hit-testing without panning: only redraws when hovered bubble changes
            var hit = HitTestBubble(currentPos);
            var hitId = hit?.Id;
            if (hitId != _currentHoveredId)
            {
                _currentHoveredId = hitId;
                _viewModel.NotifyBubbleHovered(hitId);
                ProtectedCursor = hit != null
                    ? InputSystemCursor.Create(InputSystemCursorShape.Hand)
                    : InputSystemCursor.Create(InputSystemCursorShape.Arrow);
            }
        }
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (_currentHoveredId.HasValue)
        {
            _currentHoveredId = null;
            _viewModel?.NotifyBubbleHovered(null);
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
        }
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            ReleasePointerCapture(e.Pointer);

            var point = e.GetCurrentPoint(this);
            var currentPos = new Vector2((float)point.Position.X, (float)point.Position.Y);
            if (Vector2.Distance(_pointerPressPos, currentPos) < 6f && point.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
            {
                HitTestAndSelectBubble(currentPos);
            }

            e.Handled = true;
        }
    }

    private PageOverlayItem? HitTestBubble(Vector2 screenPos)
    {
        if (_viewModel == null || _viewModel.Pages.Count == 0 || _viewModel.Overlays.Count == 0) return null;

        var worldPt = _viewModel.Transform.ScreenToWorld(screenPos);

        if (_viewModel.IsPageMode)
        {
            if (_viewModel.CurrentPageIndex >= 0 && _viewModel.CurrentPageIndex < _viewModel.Pages.Count)
            {
                var page = _viewModel.Pages[_viewModel.CurrentPageIndex];
                float px = worldPt.X;
                float py = worldPt.Y;

                if (px >= 0 && px <= page.Width && py >= 0 && py <= page.Height)
                {
                    float nx = px / page.Width;
                    float ny = py / page.Height;

                    return _viewModel.Overlays
                        .Where(o => o.PageNumber == page.PageNumber)
                        .FirstOrDefault(o => nx >= o.Bounds.X && nx <= o.Bounds.Right && ny >= o.Bounds.Y && ny <= o.Bounds.Bottom);
                }
            }
        }
        else
        {
            // Continuous mode
            foreach (var page in _viewModel.Pages)
            {
                float px = worldPt.X;
                float py = worldPt.Y - page.OffsetY;

                if (px >= 0 && px <= page.Width && py >= 0 && py <= page.Height)
                {
                    float nx = px / page.Width;
                    float ny = py / page.Height;

                    var hit = _viewModel.Overlays
                        .Where(o => o.PageNumber == page.PageNumber)
                        .FirstOrDefault(o => nx >= o.Bounds.X && nx <= o.Bounds.Right && ny >= o.Bounds.Y && ny <= o.Bounds.Bottom);

                    if (hit != null) return hit;
                }
            }
        }

        return null;
    }

    private void HitTestAndSelectBubble(Vector2 screenPos)
    {
        var hit = HitTestBubble(screenPos);
        if (hit != null)
        {
            _viewModel?.NotifyOverlayClicked(hit.Id);
        }
        else
        {
            _viewModel?.NotifyBackgroundClicked();
        }
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isPanning = false;
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (_viewModel == null) return;

        var point = e.GetCurrentPoint(this);
        var cursor = new Vector2((float)point.Position.X, (float)point.Position.Y);
        var delta = point.Properties.MouseWheelDelta;

        var isCtrlDown = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (isCtrlDown || _viewModel.IsPageMode)
        {
            // Zoom centered at mouse cursor
            _viewModel.ZoomAtCursor(cursor, delta);
        }
        else
        {
            // Continuous mode scrolling
            var scrollAmount = delta * 1.5f;
            _viewModel.UpdateContinuousScrollPosition(scrollAmount);
        }

        e.Handled = true;
    }

    private void OnDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (_viewModel == null) return;

        // Toggle Fit Page / 100% Zoom
        if (Math.Abs(_viewModel.Zoom - 1.0f) < 0.05f)
        {
            _viewModel.FitPage();
        }
        else
        {
            _viewModel.ResetZoom();
        }

        e.Handled = true;
    }

    // -------------------------------------------------------------
    // KEYBOARD NAVIGATION
    // -------------------------------------------------------------

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_viewModel == null) return;

        var isCtrl = InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        switch (e.Key)
        {
            case VirtualKey.Left:
            case VirtualKey.PageUp:
            case VirtualKey.A:
                _viewModel.PreviousPage();
                e.Handled = true;
                break;

            case VirtualKey.Right:
            case VirtualKey.PageDown:
            case VirtualKey.D:
                _viewModel.NextPage();
                e.Handled = true;
                break;

            case VirtualKey.Home:
                _viewModel.SetPage(0);
                e.Handled = true;
                break;

            case VirtualKey.End:
                _viewModel.SetPage(_viewModel.TotalPages - 1);
                e.Handled = true;
                break;

            case VirtualKey.Add:
                _viewModel.ZoomIn();
                e.Handled = true;
                break;

            case VirtualKey.Subtract:
                _viewModel.ZoomOut();
                e.Handled = true;
                break;

            case VirtualKey.Number0:
                if (isCtrl)
                {
                    _viewModel.ResetZoom();
                    e.Handled = true;
                }
                break;

            case VirtualKey.F:
                _viewModel.FitPage();
                e.Handled = true;
                break;

            case VirtualKey.W:
                _viewModel.FitWidth();
                e.Handled = true;
                break;

            case VirtualKey.M:
                _viewModel.ToggleViewMode();
                e.Handled = true;
                break;
        }
    }

    public void DisposeGpuResources()
    {
        if (_canvas != null)
        {
            _canvas.CreateResources -= OnCanvasCreateResources;
            _canvas.Draw -= OnCanvasDraw;
            _canvas.RemoveFromVisualTree();
            _canvas = null;
            Content = null;
        }

        _imageCache?.Dispose();
        _imageCache = null;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            if (_viewModel != null)
            {
                _viewModel.RedrawRequested -= OnRedrawRequested;
            }
            DisposeGpuResources();
            GC.SuppressFinalize(this);
        }
    }
}
