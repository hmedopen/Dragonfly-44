namespace Dragonfly44.Application.Rendering;

/// <summary>
/// Represents normalized coordinates (0.0 to 1.0) relative to page width and height.
/// Ensures resolution-independent storage for future bubble geometry and reading order.
/// </summary>
public readonly struct NormalizedRect : IEquatable<NormalizedRect>
{
    public float X { get; }
    public float Y { get; }
    public float Width { get; }
    public float Height { get; }

    public float Right => X + Width;
    public float Bottom => Y + Height;

    public NormalizedRect(float x, float y, float width, float height)
    {
        X = Math.Clamp(x, 0.0f, 1.0f);
        Y = Math.Clamp(y, 0.0f, 1.0f);
        Width = Math.Clamp(width, 0.0f, 1.0f - X);
        Height = Math.Clamp(height, 0.0f, 1.0f - Y);
    }

    /// <summary>
    /// Converts this normalized rectangle into absolute pixel coordinates for a page of specified dimensions.
    /// </summary>
    public (float PixelX, float PixelY, float PixelWidth, float PixelHeight) ToPixels(float pageWidth, float pageHeight)
    {
        return (
            X * pageWidth,
            Y * pageHeight,
            Width * pageWidth,
            Height * pageHeight
        );
    }

    /// <summary>
    /// Creates a normalized rectangle from pixel coordinates on a page of specified dimensions.
    /// </summary>
    public static NormalizedRect FromPixels(float px, float py, float pw, float ph, float pageWidth, float pageHeight)
    {
        if (pageWidth <= 0f || pageHeight <= 0f) return new NormalizedRect(0, 0, 0, 0);

        return new NormalizedRect(
            px / pageWidth,
            py / pageHeight,
            pw / pageWidth,
            ph / pageHeight
        );
    }

    public bool Equals(NormalizedRect other) =>
        Math.Abs(X - other.X) < 0.0001f &&
        Math.Abs(Y - other.Y) < 0.0001f &&
        Math.Abs(Width - other.Width) < 0.0001f &&
        Math.Abs(Height - other.Height) < 0.0001f;

    public override bool Equals(object? obj) => obj is NormalizedRect other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
    public static bool operator ==(NormalizedRect left, NormalizedRect right) => left.Equals(right);
    public static bool operator !=(NormalizedRect left, NormalizedRect right) => !left.Equals(right);
}

/// <summary>
/// Forward-compatible model representing an overlay element (e.g. future dialogue bubble, marker).
/// </summary>
public class PageOverlayItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int PageNumber { get; set; }
    public NormalizedRect Bounds { get; set; }
    public int ReadingOrder { get; set; }
    public bool IsSelected { get; set; }
    public bool IsHovered { get; set; }
    public bool IsEditing { get; set; }
    public string? Label { get; set; }
}
