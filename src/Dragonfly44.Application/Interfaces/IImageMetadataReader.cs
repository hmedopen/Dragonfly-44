namespace Dragonfly44.Application.Interfaces;

public record ImageMetadata(int Width, int Height, string Format, long FileSizeBytes);

public interface IImageMetadataReader
{
    bool IsSupportedImage(string filePath);
    ImageMetadata ReadMetadata(string filePath);
    Task<ImageMetadata> ReadMetadataAsync(string filePath, CancellationToken cancellationToken = default);
}
