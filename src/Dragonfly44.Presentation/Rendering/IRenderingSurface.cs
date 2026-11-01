using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;

namespace Dragonfly44.Presentation.Rendering;

public class RenderDrawEventArgs : EventArgs
{
    public CanvasDrawingSession DrawingSession { get; }
    public float Width { get; }
    public float Height { get; }
    public float Dpi { get; }

    public RenderDrawEventArgs(CanvasDrawingSession drawingSession, float width, float height, float dpi)
    {
        DrawingSession = drawingSession;
        Width = width;
        Height = height;
        Dpi = dpi;
    }
}

public class RenderResourcesEventArgs : EventArgs
{
    public CanvasControl Sender { get; }
    public CanvasCreateResourcesReason Reason { get; }

    public RenderResourcesEventArgs(CanvasControl sender, CanvasCreateResourcesReason reason)
    {
        Sender = sender;
        Reason = reason;
    }
}

public interface IRenderingSurface : IDisposable
{
    bool IsReady { get; }
    float CurrentDpi { get; }

    event EventHandler<RenderDrawEventArgs>? DrawRequested;
    event EventHandler<RenderResourcesEventArgs>? CreateResourcesRequested;

    void Invalidate();
    void DisposeGpuResources();
}
