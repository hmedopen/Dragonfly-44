using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Dragonfly44.Presentation.Rendering;

public class Win2DRenderSurface : ContentControl, IRenderingSurface
{
    private CanvasControl? _canvas;
    private bool _isDisposed;

    public bool IsReady => _canvas != null && _canvas.ReadyToDraw;
    public float CurrentDpi => _canvas?.Dpi ?? 96f;

    public event EventHandler<RenderDrawEventArgs>? DrawRequested;
    public event EventHandler<RenderResourcesEventArgs>? CreateResourcesRequested;

    public Win2DRenderSurface()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_canvas == null)
        {
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

    private void OnCanvasCreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
    {
        CreateResourcesRequested?.Invoke(this, new RenderResourcesEventArgs(sender, args.Reason));
    }

    private void OnCanvasDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var width = (float)sender.ActualWidth;
        var height = (float)sender.ActualHeight;
        var dpi = sender.Dpi;

        DrawRequested?.Invoke(this, new RenderDrawEventArgs(args.DrawingSession, width, height, dpi));
    }

    public void Invalidate()
    {
        _canvas?.Invalidate();
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
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            DisposeGpuResources();
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
