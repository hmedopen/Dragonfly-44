namespace Dragonfly44.Application.Rendering;

/// <summary>
/// Contains layout and geometric dimensions for a manga page in world coordinates.
/// </summary>
public class MangaPageLayoutInfo
{
    public Guid PageId { get; set; }
    public int PageNumber { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ImageRelativePath { get; set; } = string.Empty;
    public string FileFormat { get; set; } = string.Empty;

    public float Width { get; set; }
    public float Height { get; set; }
    public float OffsetY { get; set; }

    public float AspectRatio => Height > 0 ? Width / Height : 1.0f;

    public MangaPageLayoutInfo()
    {
    }

    public MangaPageLayoutInfo(
        Guid pageId,
        int pageNumber,
        string originalFileName,
        string imageRelativePath,
        string fileFormat,
        float width,
        float height,
        float offsetY = 0f)
    {
        PageId = pageId;
        PageNumber = pageNumber;
        OriginalFileName = originalFileName;
        ImageRelativePath = imageRelativePath;
        FileFormat = fileFormat;
        Width = width > 0 ? width : 1200f; // Default fallback width
        Height = height > 0 ? height : 1800f; // Default fallback height
        OffsetY = offsetY;
    }

    /// <summary>
    /// Checks whether this page intersects with the specified vertical world-space range.
    /// </summary>
    public bool IntersectsVerticalRange(float top, float bottom)
    {
        var pageTop = OffsetY;
        var pageBottom = OffsetY + Height;
        return pageBottom >= top && pageTop <= bottom;
    }
}
