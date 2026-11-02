namespace Dragonfly44.Application.Rendering;

/// <summary>
/// Computes vertical stacking and viewport-intersection logic for continuous reading mode.
/// </summary>
public class ContinuousLayoutCalculator
{
    public const float DefaultPageGap = 20.0f;

    public float PageGap { get; set; } = DefaultPageGap;

    /// <summary>
    /// Computes the vertical offsets for all pages in sequence.
    /// Pages are stacked vertically with PageGap between them.
    /// </summary>
    public float LayoutPages(IReadOnlyList<MangaPageLayoutInfo> pages, float targetWidth = 0f)
    {
        if (pages == null || pages.Count == 0) return 0f;

        float currentY = 0f;
        for (int i = 0; i < pages.Count; i++)
        {
            var p = pages[i];
            if (targetWidth > 0 && p.Width > 0)
            {
                var ratio = targetWidth / p.Width;
                p.Width = targetWidth;
                p.Height = p.Height * ratio;
            }

            p.OffsetY = currentY;
            currentY += p.Height + (i < pages.Count - 1 ? PageGap : 0f);
        }

        return currentY;
    }

    /// <summary>
    /// Returns the indices of all pages intersecting the visible world vertical viewport.
    /// Includes a pre-fetch buffer above and below to prevent pop-in during scrolling.
    /// </summary>
    public IReadOnlyList<int> GetVisiblePageIndices(
        IReadOnlyList<MangaPageLayoutInfo> pages,
        float worldTop,
        float worldBottom,
        float bufferPixels = 300f)
    {
        var result = new List<int>();
        if (pages == null || pages.Count == 0) return result;

        var queryTop = worldTop - bufferPixels;
        var queryBottom = worldBottom + bufferPixels;

        for (int i = 0; i < pages.Count; i++)
        {
            if (pages[i].IntersectsVerticalRange(queryTop, queryBottom))
            {
                result.Add(i);
            }
        }

        return result;
    }

    /// <summary>
    /// Finds the index of the primary page currently most visible in the viewport center.
    /// </summary>
    public int GetPrimaryPageIndexAt(IReadOnlyList<MangaPageLayoutInfo> pages, float worldCenterY)
    {
        if (pages == null || pages.Count == 0) return 0;

        for (int i = 0; i < pages.Count; i++)
        {
            var p = pages[i];
            if (worldCenterY >= p.OffsetY && worldCenterY <= p.OffsetY + p.Height + PageGap)
            {
                return i;
            }
        }

        if (worldCenterY < pages[0].OffsetY) return 0;
        return pages.Count - 1;
    }

    /// <summary>
    /// Gets the top Y offset of a given page index in continuous layout.
    /// </summary>
    public float GetPageOffset(IReadOnlyList<MangaPageLayoutInfo> pages, int pageIndex)
    {
        if (pages == null || pages.Count == 0) return 0f;
        var clamped = Math.Clamp(pageIndex, 0, pages.Count - 1);
        return pages[clamped].OffsetY;
    }
}
