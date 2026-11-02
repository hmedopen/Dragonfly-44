using Microsoft.Graphics.Canvas;

namespace Dragonfly44.Presentation.Rendering;

/// <summary>
/// Interface for asynchronous manga image loading and GPU texture creation.
/// </summary>
public interface IMangaImageLoader
{
    /// <summary>
    /// Asynchronously loads an image from disk into a hardware-accelerated CanvasBitmap.
    /// Returns null or a fallback placeholder if the file is missing or corrupted.
    /// </summary>
    Task<CanvasBitmap?> LoadBitmapAsync(
        ICanvasResourceCreator resourceCreator,
        string absolutePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a diagnostic placeholder bitmap for missing or corrupt files.
    /// </summary>
    CanvasBitmap CreateFallbackBitmap(
        ICanvasResourceCreator resourceCreator,
        float width,
        float height,
        string message);
}
