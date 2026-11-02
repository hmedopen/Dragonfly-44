namespace Dragonfly44.Application.Rendering;

/// <summary>
/// Specifies the display mode of the manga canvas.
/// </summary>
public enum CanvasViewMode
{
    /// <summary>
    /// Displays one manga page at a time with previous/next navigation.
    /// </summary>
    Page,

    /// <summary>
    /// Displays all chapter pages vertically in a continuous scroll layout.
    /// </summary>
    Continuous
}
