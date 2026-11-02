using System.Numerics;
using Dragonfly44.Application.Rendering;
using Microsoft.Graphics.Canvas;
using Windows.UI;

namespace Dragonfly44.Presentation.Rendering;

/// <summary>
/// Hardware-accelerated renderer for manga pages and future overlay elements.
/// Ensures aspect ratios are strictly preserved and never stretched.
/// </summary>
public class MangaCanvasRenderer
{
    private readonly Color _canvasBgColor = Color.FromArgb(255, 18, 18, 20);
    private readonly Color _pageBorderColor = Color.FromArgb(60, 255, 255, 255);
    private readonly Color _pageDropShadowColor = Color.FromArgb(120, 0, 0, 0);

    /// <summary>
    /// Renders the manga canvas according to the current view mode and transform.
    /// </summary>
    public void Draw(
        CanvasDrawingSession ds,
        float viewportWidth,
        float viewportHeight,
        CanvasViewMode mode,
        CanvasTransform transform,
        IReadOnlyList<MangaPageLayoutInfo> pages,
        int activePageIndex,
        Func<string, CanvasBitmap?> bitmapProvider,
        IReadOnlyList<PageOverlayItem>? overlays = null)
    {
        // 1. Clear background with dark neutral tone
        ds.Clear(_canvasBgColor);

        if (pages == null || pages.Count == 0)
        {
            return;
        }

        // 2. Set world transform matrix (Zoom & Pan)
        ds.Transform = transform.GetMatrix();

        if (mode == CanvasViewMode.Page)
        {
            DrawPageMode(ds, pages, activePageIndex, bitmapProvider, overlays);
        }
        else
        {
            DrawContinuousMode(ds, transform, viewportWidth, viewportHeight, pages, bitmapProvider, overlays);
        }

        // Reset transform to identity for any screen-space UI elements if needed
        ds.Transform = Matrix3x2.Identity;
    }

    private void DrawPageMode(
        CanvasDrawingSession ds,
        IReadOnlyList<MangaPageLayoutInfo> pages,
        int activePageIndex,
        Func<string, CanvasBitmap?> bitmapProvider,
        IReadOnlyList<PageOverlayItem>? overlays)
    {
        var clampedIndex = Math.Clamp(activePageIndex, 0, pages.Count - 1);
        var page = pages[clampedIndex];

        // Draw page shadow
        ds.FillRectangle(4, 4, page.Width, page.Height, _pageDropShadowColor);

        // Retrieve bitmap
        var bitmap = bitmapProvider(page.ImageRelativePath);
        if (bitmap != null)
        {
            // Draw page maintaining exact native aspect ratio
            ds.DrawImage(bitmap, new Windows.Foundation.Rect(0, 0, page.Width, page.Height));
        }
        else
        {
            // Placeholder rectangle while loading
            ds.FillRectangle(0, 0, page.Width, page.Height, Color.FromArgb(255, 32, 32, 35));
        }

        // Draw crisp page border
        ds.DrawRectangle(0, 0, page.Width, page.Height, _pageBorderColor, 1.0f);

        // Future overlay compatibility hook
        if (overlays != null && overlays.Count > 0)
        {
            RenderOverlayItems(ds, page.Width, page.Height, 0f, page.PageNumber, overlays);
        }
    }

    private void DrawContinuousMode(
        CanvasDrawingSession ds,
        CanvasTransform transform,
        float viewportWidth,
        float viewportHeight,
        IReadOnlyList<MangaPageLayoutInfo> pages,
        Func<string, CanvasBitmap?> bitmapProvider,
        IReadOnlyList<PageOverlayItem>? overlays)
    {
        // Calculate world bounds of current viewport
        var worldTopLeft = transform.ScreenToWorld(Vector2.Zero);
        var worldBottomRight = transform.ScreenToWorld(new Vector2(viewportWidth, viewportHeight));

        var visibleTop = Math.Min(worldTopLeft.Y, worldBottomRight.Y);
        var visibleBottom = Math.Max(worldTopLeft.Y, worldBottomRight.Y);

        for (int i = 0; i < pages.Count; i++)
        {
            var page = pages[i];

            // Viewport culling: only draw pages that intersect the viewport
            if (!page.IntersectsVerticalRange(visibleTop, visibleBottom))
            {
                continue;
            }

            var destRect = new Windows.Foundation.Rect(0, page.OffsetY, page.Width, page.Height);

            // Draw subtle page shadow
            ds.FillRectangle(4, page.OffsetY + 4, page.Width, page.Height, _pageDropShadowColor);

            var bitmap = bitmapProvider(page.ImageRelativePath);
            if (bitmap != null)
            {
                ds.DrawImage(bitmap, destRect);
            }
            else
            {
                ds.FillRectangle(destRect, Color.FromArgb(255, 32, 32, 35));
            }

            // Crisp page border
            ds.DrawRectangle(destRect, _pageBorderColor, 1.0f);

            // Future overlay compatibility hook
            if (overlays != null && overlays.Count > 0)
            {
                RenderOverlayItems(ds, page.Width, page.Height, page.OffsetY, page.PageNumber, overlays);
            }
        }
    }

    /// <summary>
    /// Forward-compatible overlay rendering hook for dialogue bubbles and markers.
    /// Supports Normal, Hovered, Selected, and Active Editing states with distinct visual cues.
    /// Uses normalized coordinates mapped to current page dimensions and offset.
    /// </summary>
    public void RenderOverlayItems(
        CanvasDrawingSession ds,
        float pageWidth,
        float pageHeight,
        float pageOffsetY,
        int pageNumber,
        IReadOnlyList<PageOverlayItem> overlays)
    {
        var normalBorder = Color.FromArgb(140, 100, 115, 130);
        var normalFill = Color.FromArgb(20, 0, 122, 255);

        var hoverBorder = Color.FromArgb(220, 80, 160, 255);
        var hoverFill = Color.FromArgb(45, 80, 160, 255);

        var selectedBorder = Color.FromArgb(255, 0, 122, 255);
        var selectedFill = Color.FromArgb(55, 0, 122, 255);

        var editingBorder = Color.FromArgb(255, 16, 185, 129);
        var editingFill = Color.FromArgb(65, 16, 185, 129);

        using var badgeTextFormat = new Microsoft.Graphics.Canvas.Text.CanvasTextFormat
        {
            FontSize = 11f,
            FontFamily = "Segoe UI",
            WordWrapping = Microsoft.Graphics.Canvas.Text.CanvasWordWrapping.NoWrap
        };

        foreach (var item in overlays)
        {
            if (item.PageNumber != pageNumber) continue;

            var (px, py, pw, ph) = item.Bounds.ToPixels(pageWidth, pageHeight);
            var rect = new Windows.Foundation.Rect(px, pageOffsetY + py, pw, ph);

            Color borderColor;
            Color fillColor;
            Color badgeBgColor;
            float strokeWidth;

            if (item.IsEditing)
            {
                borderColor = editingBorder;
                fillColor = editingFill;
                badgeBgColor = editingBorder;
                strokeWidth = 3.0f;
            }
            else if (item.IsSelected)
            {
                borderColor = selectedBorder;
                fillColor = selectedFill;
                badgeBgColor = selectedBorder;
                strokeWidth = 2.5f;
            }
            else if (item.IsHovered)
            {
                borderColor = hoverBorder;
                fillColor = hoverFill;
                badgeBgColor = hoverBorder;
                strokeWidth = 2.0f;
            }
            else
            {
                borderColor = normalBorder;
                fillColor = normalFill;
                badgeBgColor = Color.FromArgb(160, 30, 40, 50);
                strokeWidth = 1.2f;
            }

            // Draw bubble highlight fill and boundary stroke
            ds.FillRectangle(rect, fillColor);
            ds.DrawRectangle(rect, borderColor, strokeWidth);

            // Draw reading order badge pill in top-left corner
            string badgeText = item.IsEditing
                ? $"#{item.ReadingOrder} [Edit]"
                : $"#{item.ReadingOrder}";

            float badgeWidth = item.IsEditing ? 56f : 28f;
            float badgeHeight = 16f;
            var badgeRect = new Windows.Foundation.Rect(px, pageOffsetY + py, Math.Min(badgeWidth, pw), Math.Min(badgeHeight, ph));

            ds.FillRectangle(badgeRect, badgeBgColor);
            ds.DrawText(badgeText, (float)badgeRect.X + 3f, (float)badgeRect.Y + 1f, Color.FromArgb(255, 255, 255, 255), badgeTextFormat);
        }
    }
}
