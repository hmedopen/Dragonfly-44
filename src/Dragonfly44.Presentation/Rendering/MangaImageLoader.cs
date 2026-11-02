using Microsoft.Extensions.Logging;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.UI;

namespace Dragonfly44.Presentation.Rendering;

/// <summary>
/// Hardware-accelerated manga image loader supporting PNG, JPG, JPEG, and WebP.
/// Implements resilient decoding, cancellation, and graceful fallback handling.
/// </summary>
public class MangaImageLoader : IMangaImageLoader
{
    private readonly ILogger<MangaImageLoader> _logger;

    public MangaImageLoader(ILogger<MangaImageLoader> logger)
    {
        _logger = logger;
    }

    public async Task<CanvasBitmap?> LoadBitmapAsync(
        ICanvasResourceCreator resourceCreator,
        string absolutePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            _logger.LogWarning("LoadBitmapAsync called with empty path");
            return CreateFallbackBitmap(resourceCreator, 1200, 1800, "Image path is empty");
        }

        if (!File.Exists(absolutePath))
        {
            _logger.LogWarning("Manga image file not found at path: {Path}", absolutePath);
            return CreateFallbackBitmap(resourceCreator, 1200, 1800, $"File not found:\n{Path.GetFileName(absolutePath)}");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Load directly from local storage using Direct2D/WIC
            var bitmap = await CanvasBitmap.LoadAsync(resourceCreator, absolutePath);
            return bitmap;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Image decoding cancelled for: {Path}", absolutePath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decode manga image from: {Path}", absolutePath);
            return CreateFallbackBitmap(resourceCreator, 1200, 1800, $"Corrupted or unsupported:\n{Path.GetFileName(absolutePath)}\n{ex.Message}");
        }
    }

    public CanvasBitmap CreateFallbackBitmap(
        ICanvasResourceCreator resourceCreator,
        float width,
        float height,
        string message)
    {
        var targetW = width > 100 ? width : 1200f;
        var targetH = height > 100 ? height : 1800f;

        var target = new CanvasRenderTarget(resourceCreator, targetW, targetH, 96f);
        using (var ds = target.CreateDrawingSession())
        {
            // Dark neutral placeholder surface
            ds.Clear(Color.FromArgb(255, 28, 28, 30));

            // Subtle border
            ds.DrawRectangle(4, 4, targetW - 8, targetH - 8, Color.FromArgb(255, 60, 60, 65), 2.0f);

            // Diagonal warning lines
            ds.DrawLine(20, 20, targetW - 20, targetH - 20, Color.FromArgb(80, 255, 69, 58), 1.5f);
            ds.DrawLine(targetW - 20, 20, 20, targetH - 20, Color.FromArgb(80, 255, 69, 58), 1.5f);

            // Informational text
            using var textFormat = new CanvasTextFormat
            {
                FontSize = 24,
                WordWrapping = CanvasWordWrapping.Wrap,
                HorizontalAlignment = CanvasHorizontalAlignment.Center,
                VerticalAlignment = CanvasVerticalAlignment.Center
            };

            ds.DrawText(message, new System.Numerics.Vector2(targetW * 0.5f, targetH * 0.5f), Color.FromArgb(255, 235, 235, 245), textFormat);
        }

        return target;
    }
}
