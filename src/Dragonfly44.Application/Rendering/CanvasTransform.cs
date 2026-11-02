using System.Numerics;

namespace Dragonfly44.Application.Rendering;

/// <summary>
/// Represents and calculates view transformations (zoom and pan) for the manga canvas.
/// All transformations preserve aspect ratios and support cursor-centered scaling.
/// </summary>
public class CanvasTransform
{
    public const float DefaultMinZoom = 0.1f;
    public const float DefaultMaxZoom = 10.0f;
    public const float DefaultZoom = 1.0f;

    private float _zoom = DefaultZoom;
    private Vector2 _pan = Vector2.Zero;

    public float MinZoom { get; set; } = DefaultMinZoom;
    public float MaxZoom { get; set; } = DefaultMaxZoom;

    public float Zoom
    {
        get => _zoom;
        set => _zoom = Math.Clamp(value, MinZoom, MaxZoom);
    }

    public Vector2 Pan
    {
        get => _pan;
        set => _pan = value;
    }

    public CanvasTransform()
    {
    }

    public CanvasTransform(float zoom, Vector2 pan)
    {
        Zoom = zoom;
        Pan = pan;
    }

    /// <summary>
    /// Gets the 3x2 affine transformation matrix for Direct2D rendering.
    /// Transform order: Scale then Translate.
    /// </summary>
    public Matrix3x2 GetMatrix()
    {
        return Matrix3x2.CreateScale(Zoom) * Matrix3x2.CreateTranslation(Pan);
    }

    /// <summary>
    /// Maps a screen/viewport coordinate point to world/page space coordinates.
    /// </summary>
    public Vector2 ScreenToWorld(Vector2 screenPoint)
    {
        if (Zoom <= 0f) return Vector2.Zero;
        return (screenPoint - Pan) / Zoom;
    }

    /// <summary>
    /// Maps a world/page space point to screen/viewport coordinates.
    /// </summary>
    public Vector2 WorldToScreen(Vector2 worldPoint)
    {
        return (worldPoint * Zoom) + Pan;
    }

    /// <summary>
    /// Adjusts zoom while keeping the world point under the cursor stationary on screen.
    /// </summary>
    public void ZoomAt(Vector2 cursorScreenPoint, float newZoom)
    {
        var clampedZoom = Math.Clamp(newZoom, MinZoom, MaxZoom);
        if (Math.Abs(clampedZoom - _zoom) < 0.0001f) return;

        // World point before zoom: W = (S - Pan) / Zoom
        // S = W * newZoom + newPan => newPan = S - W * newZoom = S - (S - Pan) * (newZoom / Zoom)
        var scaleRatio = clampedZoom / _zoom;
        _pan = cursorScreenPoint - ((cursorScreenPoint - _pan) * scaleRatio);
        _zoom = clampedZoom;
    }

    /// <summary>
    /// Calculates the optimal zoom and pan to center and fit an entire page inside the viewport.
    /// </summary>
    public static (float Zoom, Vector2 Pan) CalculateFitPage(
        float viewportWidth,
        float viewportHeight,
        float pageWidth,
        float pageHeight,
        float padding = 20f)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0 || pageWidth <= 0 || pageHeight <= 0)
        {
            return (DefaultZoom, Vector2.Zero);
        }

        var availableW = Math.Max(1f, viewportWidth - (padding * 2f));
        var availableH = Math.Max(1f, viewportHeight - (padding * 2f));

        var scaleX = availableW / pageWidth;
        var scaleY = availableH / pageHeight;
        var fitZoom = Math.Clamp(Math.Min(scaleX, scaleY), DefaultMinZoom, DefaultMaxZoom);

        var renderedW = pageWidth * fitZoom;
        var renderedH = pageHeight * fitZoom;

        var panX = (viewportWidth - renderedW) * 0.5f;
        var panY = (viewportHeight - renderedH) * 0.5f;

        return (fitZoom, new Vector2(panX, panY));
    }

    /// <summary>
    /// Calculates the optimal zoom and pan so that the page width fills the viewport width.
    /// </summary>
    public static (float Zoom, Vector2 Pan) CalculateFitWidth(
        float viewportWidth,
        float viewportHeight,
        float pageWidth,
        float pageHeight,
        float padding = 20f)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0 || pageWidth <= 0 || pageHeight <= 0)
        {
            return (DefaultZoom, Vector2.Zero);
        }

        var availableW = Math.Max(1f, viewportWidth - (padding * 2f));
        var fitZoom = Math.Clamp(availableW / pageWidth, DefaultMinZoom, DefaultMaxZoom);

        var renderedW = pageWidth * fitZoom;
        var renderedH = pageHeight * fitZoom;

        var panX = (viewportWidth - renderedW) * 0.5f;
        // In fit width, align top with padding or center if page height fits
        var panY = (renderedH <= viewportHeight) ? (viewportHeight - renderedH) * 0.5f : padding;

        return (fitZoom, new Vector2(panX, panY));
    }

    /// <summary>
    /// Resets zoom to 1.0 (100%) and centers the page in the viewport.
    /// </summary>
    public void Reset(float viewportWidth, float viewportHeight, float pageWidth, float pageHeight)
    {
        Zoom = DefaultZoom;
        if (viewportWidth > 0 && viewportHeight > 0 && pageWidth > 0 && pageHeight > 0)
        {
            _pan = new Vector2(
                (viewportWidth - pageWidth) * 0.5f,
                (viewportHeight - pageHeight) * 0.5f);
        }
        else
        {
            _pan = Vector2.Zero;
        }
    }
}
