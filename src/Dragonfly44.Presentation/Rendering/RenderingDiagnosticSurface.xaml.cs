using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI;

namespace Dragonfly44.Presentation.Rendering;

public sealed partial class RenderingDiagnosticSurface : UserControl
{
    private readonly VectorRenderer _renderer = new();
    private long _drawCount;

    public RenderingDiagnosticSurface()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RenderSurface.CreateResourcesRequested += OnCreateResources;
        RenderSurface.DrawRequested += OnDraw;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        RenderSurface.CreateResourcesRequested -= OnCreateResources;
        RenderSurface.DrawRequested -= OnDraw;
        RenderSurface.DisposeGpuResources();
    }

    private void OnCreateResources(object? sender, RenderResourcesEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            StatusBadge.Text = $"Device Ready ({e.Reason})";
        });
    }

    private void OnDraw(object? sender, RenderDrawEventArgs e)
    {
        _drawCount++;
        var ds = e.DrawingSession;
        var w = e.Width;
        var h = e.Height;

        // 1. Clear background
        ds.Clear(Color.FromArgb(255, 12, 12, 14));

        // 2. Draw neutral technical background grid
        var gridColor = Color.FromArgb(40, 255, 255, 255);
        _renderer.DrawGrid(ds, w, h, 20f, gridColor, 0.5f);

        // 3. Draw vector rounded test card
        var cardFill = Color.FromArgb(50, 74, 107, 130);
        var cardBorder = Color.FromArgb(160, 74, 107, 130);
        var cardX = Math.Max(20f, w * 0.08f);
        var cardY = Math.Max(20f, h * 0.15f);
        var cardW = Math.Max(120f, w * 0.35f);
        var cardH = Math.Max(60f, h * 0.65f);
        _renderer.DrawRoundedCard(ds, cardX, cardY, cardW, cardH, 6f, cardFill, cardBorder, 1.2f);

        // 4. Draw cubic Bezier curve across canvas
        var start = new Vector2(cardX + cardW + 20f, h * 0.7f);
        var cp1 = new Vector2(start.X + (w - start.X) * 0.25f, h * 0.1f);
        var cp2 = new Vector2(start.X + (w - start.X) * 0.75f, h * 0.9f);
        var end = new Vector2(Math.Max(start.X + 60f, w - 30f), h * 0.3f);
        var curveColor = Color.FromArgb(220, 16, 185, 129); // Success placeholder
        _renderer.DrawCubicBezier(ds, start, cp1, cp2, end, curveColor, 2f);

        // 5. Draw crosshairs at key control points
        var crosshairColor = Color.FromArgb(180, 245, 158, 11); // Warning placeholder
        _renderer.DrawTechnicalCrosshair(ds, cp1, 8f, crosshairColor, 1f);
        _renderer.DrawTechnicalCrosshair(ds, cp2, 8f, crosshairColor, 1f);

        // Update UI metrics on dispatcher
        DispatcherQueue.TryEnqueue(() =>
        {
            MetricsDrawCount.Text = $"Draw Cycles: {_drawCount}";
            MetricsDpi.Text = $"DPI: {e.Dpi:F0} ({(e.Dpi / 96.0f):F2}x)";
            MetricsResolution.Text = $"Surface: {w:F0} x {h:F0}";
            StatusBadge.Text = "Hardware Accelerated (Direct2D / Win2D)";
        });
    }

    private void OnRedrawClicked(object sender, RoutedEventArgs e)
    {
        RenderSurface.Invalidate();
    }
}
