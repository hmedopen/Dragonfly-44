namespace Dragonfly44.Domain.Contracts;

public record DetectedBubbleDto(
    double X,
    double Y,
    double Width,
    double Height,
    string RawText,
    double Confidence,
    string? ShapeHint = null
);

public record ScanResult(
    bool Success,
    IReadOnlyList<DetectedBubbleDto> DetectedBubbles,
    string? ErrorMessage = null
)
{
    public static ScanResult Succeeded(IReadOnlyList<DetectedBubbleDto> bubbles) =>
        new(true, bubbles);

    public static ScanResult Failed(string error) =>
        new(false, Array.Empty<DetectedBubbleDto>(), error);
}
