using System.Buffers.Binary;
using Dragonfly44.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Dragonfly44.Infrastructure.Imaging;

public class ImageMetadataReader : IImageMetadataReader
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp"
    };

    private readonly ILogger<ImageMetadataReader> _logger;

    public ImageMetadataReader(ILogger<ImageMetadataReader> logger)
    {
        _logger = logger;
    }

    public bool IsSupportedImage(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        var ext = Path.GetExtension(filePath);
        return SupportedExtensions.Contains(ext);
    }

    public ImageMetadata ReadMetadata(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new ImageMetadata(0, 0, "UNKNOWN", 0);
        }

        var fileInfo = new FileInfo(filePath);
        var fileSize = fileInfo.Length;
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(stream);

            switch (ext)
            {
                case ".png":
                    return ReadPng(reader, fileSize);
                case ".jpg":
                case ".jpeg":
                    return ReadJpeg(reader, fileSize);
                case ".webp":
                    return ReadWebp(reader, fileSize);
                default:
                    return new ImageMetadata(0, 0, ext.TrimStart('.').ToUpperInvariant(), fileSize);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read image dimensions for {FilePath}", filePath);
            return new ImageMetadata(0, 0, ext.TrimStart('.').ToUpperInvariant(), fileSize);
        }
    }

    public Task<ImageMetadata> ReadMetadataAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ReadMetadata(filePath));
    }

    private static ImageMetadata ReadPng(BinaryReader reader, long fileSize)
    {
        if (reader.BaseStream.Length < 24)
            return new ImageMetadata(0, 0, "PNG", fileSize);

        var header = reader.ReadBytes(8);
        var isPng = header.Length == 8 &&
                    header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                    header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;

        if (!isPng)
            return new ImageMetadata(0, 0, "PNG", fileSize);

        // Next 4 bytes: length of IHDR
        _ = reader.ReadInt32();
        // Next 4 bytes: chunk type "IHDR"
        var chunkType = reader.ReadBytes(4);
        if (chunkType[0] != 'I' || chunkType[1] != 'H' || chunkType[2] != 'D' || chunkType[3] != 'R')
            return new ImageMetadata(0, 0, "PNG", fileSize);

        var widthBytes = reader.ReadBytes(4);
        var heightBytes = reader.ReadBytes(4);

        int width = BinaryPrimitives.ReadInt32BigEndian(widthBytes);
        int height = BinaryPrimitives.ReadInt32BigEndian(heightBytes);

        return new ImageMetadata(width, height, "PNG", fileSize);
    }

    private static ImageMetadata ReadJpeg(BinaryReader reader, long fileSize)
    {
        var stream = reader.BaseStream;
        if (stream.Length < 4)
            return new ImageMetadata(0, 0, "JPEG", fileSize);

        var soi1 = reader.ReadByte();
        var soi2 = reader.ReadByte();
        if (soi1 != 0xFF || soi2 != 0xD8)
            return new ImageMetadata(0, 0, "JPEG", fileSize);

        while (stream.Position < stream.Length - 1)
        {
            byte markerPrefix = reader.ReadByte();
            if (markerPrefix != 0xFF) continue;

            byte marker = reader.ReadByte();
            while (marker == 0xFF && stream.Position < stream.Length)
            {
                marker = reader.ReadByte();
            }

            if (marker == 0xD9 || marker == 0xDA) // EOI or SOS
                break;

            // SOF0 through SOF15 except DHT (0xC4), JPG (0xC8), DAC (0xCC)
            bool isSof = (marker >= 0xC0 && marker <= 0xC3) ||
                         (marker >= 0xC5 && marker <= 0xC7) ||
                         (marker >= 0xC9 && marker <= 0xCB) ||
                         (marker >= 0xCD && marker <= 0xCF);

            var lenBytes = reader.ReadBytes(2);
            if (lenBytes.Length < 2) break;
            ushort segmentLength = BinaryPrimitives.ReadUInt16BigEndian(lenBytes);

            if (isSof)
            {
                if (stream.Position + 5 > stream.Length) break;
                _ = reader.ReadByte(); // precision
                var hBytes = reader.ReadBytes(2);
                var wBytes = reader.ReadBytes(2);

                int height = BinaryPrimitives.ReadUInt16BigEndian(hBytes);
                int width = BinaryPrimitives.ReadUInt16BigEndian(wBytes);

                return new ImageMetadata(width, height, "JPEG", fileSize);
            }

            // Skip remaining segment
            int toSkip = segmentLength - 2;
            if (toSkip > 0 && stream.Position + toSkip <= stream.Length)
            {
                stream.Seek(toSkip, SeekOrigin.Current);
            }
            else
            {
                break;
            }
        }

        return new ImageMetadata(0, 0, "JPEG", fileSize);
    }

    private static ImageMetadata ReadWebp(BinaryReader reader, long fileSize)
    {
        var stream = reader.BaseStream;
        if (stream.Length < 16)
            return new ImageMetadata(0, 0, "WEBP", fileSize);

        var riff = reader.ReadBytes(4);
        if (riff[0] != 'R' || riff[1] != 'I' || riff[2] != 'F' || riff[3] != 'F')
            return new ImageMetadata(0, 0, "WEBP", fileSize);

        _ = reader.ReadUInt32(); // File length - 8
        var webp = reader.ReadBytes(4);
        if (webp[0] != 'W' || webp[1] != 'E' || webp[2] != 'B' || webp[3] != 'P')
            return new ImageMetadata(0, 0, "WEBP", fileSize);

        var fourCc = reader.ReadBytes(4);
        var chunkSize = reader.ReadUInt32();

        string fourCcStr = System.Text.Encoding.ASCII.GetString(fourCc);

        if (fourCcStr == "VP8 " && stream.Length >= stream.Position + 10)
        {
            // Lossy VP8
            _ = reader.ReadBytes(3); // Frame tag
            var sig = reader.ReadBytes(3);
            if (sig[0] == 0x9D && sig[1] == 0x01 && sig[2] == 0x2A)
            {
                ushort rawWidth = reader.ReadUInt16();
                ushort rawHeight = reader.ReadUInt16();
                int width = rawWidth & 0x3FFF;
                int height = rawHeight & 0x3FFF;
                return new ImageMetadata(width, height, "WEBP", fileSize);
            }
        }
        else if (fourCcStr == "VP8L" && stream.Length >= stream.Position + 5)
        {
            // Lossless VP8L
            byte sig = reader.ReadByte();
            if (sig == 0x2F)
            {
                uint packed = reader.ReadUInt32();
                int width = (int)(packed & 0x3FFF) + 1;
                int height = (int)((packed >> 14) & 0x3FFF) + 1;
                return new ImageMetadata(width, height, "WEBP", fileSize);
            }
        }
        else if (fourCcStr == "VP8X" && stream.Length >= stream.Position + 10)
        {
            // Extended VP8X
            _ = reader.ReadBytes(4); // Flags
            var wBytes = reader.ReadBytes(3);
            var hBytes = reader.ReadBytes(3);

            int width = 1 + (wBytes[0] | (wBytes[1] << 8) | (wBytes[2] << 16));
            int height = 1 + (hBytes[0] | (hBytes[1] << 8) | (hBytes[2] << 16));
            return new ImageMetadata(width, height, "WEBP", fileSize);
        }

        return new ImageMetadata(0, 0, "WEBP", fileSize);
    }
}
