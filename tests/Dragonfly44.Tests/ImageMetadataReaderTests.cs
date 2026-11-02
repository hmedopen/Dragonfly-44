using Dragonfly44.Infrastructure.Imaging;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dragonfly44.Tests;

public class ImageMetadataReaderTests : IDisposable
{
    private readonly ImageMetadataReader _reader = new(NullLogger<ImageMetadataReader>.Instance);
    private readonly string _tempDir;

    public ImageMetadataReaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"dragonfly44_meta_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task ReadMetadataAsync_ShouldParseValidPng()
    {
        var filePath = Path.Combine(_tempDir, "sample.png");
        using (var stream = File.Create(filePath))
        {
            stream.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
            stream.Write(new byte[] { 0x00, 0x00, 0x00, 0x0D }); // length 13
            stream.Write(new byte[] { 0x49, 0x48, 0x44, 0x52 }); // "IHDR"
            stream.Write(new byte[] { 0x00, 0x00, 0x07, 0x80 }); // 1920
            stream.Write(new byte[] { 0x00, 0x00, 0x04, 0x38 }); // 1080
            stream.Write(new byte[] { 0x08, 0x06, 0x00, 0x00, 0x00 });
            stream.Write(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        }

        var isSupported = _reader.IsSupportedImage(filePath);
        var result = await _reader.ReadMetadataAsync(filePath);

        isSupported.Should().BeTrue();
        result.Format.Should().Be("PNG");
        result.Width.Should().Be(1920);
        result.Height.Should().Be(1080);
        result.FileSizeBytes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ReadMetadataAsync_ShouldParseValidJpeg()
    {
        var filePath = Path.Combine(_tempDir, "photo.jpeg");
        using (var stream = File.Create(filePath))
        {
            stream.Write(new byte[] { 0xFF, 0xD8 }); // SOI
            stream.Write(new byte[] { 0xFF, 0xC0 }); // SOF0
            stream.Write(new byte[] { 0x00, 0x11 }); // Segment length (17)
            stream.WriteByte(0x08);                  // Precision 8 bits
            stream.Write(new byte[] { 0x03, 0x20 }); // Height: 800
            stream.Write(new byte[] { 0x02, 0x58 }); // Width: 600
            stream.Write(new byte[] { 0x03, 0x01, 0x11, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01 });
            stream.Write(new byte[] { 0xFF, 0xD9 }); // EOI
        }

        var isSupported = _reader.IsSupportedImage(filePath);
        var result = await _reader.ReadMetadataAsync(filePath);

        isSupported.Should().BeTrue();
        result.Format.Should().Be("JPEG");
        result.Width.Should().Be(600);
        result.Height.Should().Be(800);
    }

    [Fact]
    public async Task ReadMetadataAsync_ShouldParseValidWebpExtended()
    {
        var filePath = Path.Combine(_tempDir, "art.webp");
        using (var stream = File.Create(filePath))
        {
            stream.Write(new byte[] { 0x52, 0x49, 0x46, 0x46 }); // "RIFF"
            stream.Write(new byte[] { 0x24, 0x00, 0x00, 0x00 }); // file size - 8
            stream.Write(new byte[] { 0x57, 0x45, 0x42, 0x50 }); // "WEBP"
            stream.Write(new byte[] { 0x56, 0x50, 0x38, 0x58 }); // "VP8X"
            stream.Write(new byte[] { 0x0A, 0x00, 0x00, 0x00 }); // chunk size 10
            stream.Write(new byte[] { 0x00, 0x00, 0x00, 0x00 }); // flags
            stream.Write(new byte[] { 0xAF, 0x04, 0x00 });       // width - 1 (1199 -> 1200)
            stream.Write(new byte[] { 0x3F, 0x06, 0x00 });       // height - 1 (1599 -> 1600)
        }

        var isSupported = _reader.IsSupportedImage(filePath);
        var result = await _reader.ReadMetadataAsync(filePath);

        isSupported.Should().BeTrue();
        result.Format.Should().Be("WEBP");
        result.Width.Should().Be(1200);
        result.Height.Should().Be(1600);
    }

    [Fact]
    public void ReadMetadata_ShouldRejectUnsupportedFormat()
    {
        var filePath = Path.Combine(_tempDir, "readme.txt");
        File.WriteAllText(filePath, "Not an image at all, just plain text.");

        var isSupported = _reader.IsSupportedImage(filePath);
        var result = _reader.ReadMetadata(filePath);

        isSupported.Should().BeFalse();
        result.Width.Should().Be(0);
        result.Height.Should().Be(0);
    }

    [Fact]
    public void ReadMetadata_ShouldHandleCorruptHeaderGracefully()
    {
        var filePath = Path.Combine(_tempDir, "corrupt.png");
        File.WriteAllBytes(filePath, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x00, 0x00 }); // Truncated PNG

        var isSupported = _reader.IsSupportedImage(filePath);
        var result = _reader.ReadMetadata(filePath);

        isSupported.Should().BeTrue();
        result.Format.Should().Be("PNG");
        result.Width.Should().Be(0);
        result.Height.Should().Be(0);
    }
}
