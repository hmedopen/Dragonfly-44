using System.Runtime.InteropServices.WindowsRuntime;
using Dragonfly44.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

namespace Dragonfly44.Presentation.Input;

public class ClipboardService : IClipboardService
{
    private readonly ILogger<ClipboardService> _logger;

    public ClipboardService(ILogger<ClipboardService> logger)
    {
        _logger = logger;
    }

    public async Task<string?> GetTextAsync()
    {
        try
        {
            var dataPackage = Clipboard.GetContent();
            if (dataPackage.Contains(StandardDataFormats.Text))
            {
                return await dataPackage.GetTextAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read text from clipboard");
        }

        return null;
    }

    public Task SetTextAsync(string text)
    {
        try
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(text ?? string.Empty);
            Clipboard.SetContent(dataPackage);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set text to clipboard");
        }

        return Task.CompletedTask;
    }

    public Task<bool> HasTextAsync()
    {
        try
        {
            var content = Clipboard.GetContent();
            return Task.FromResult(content.Contains(StandardDataFormats.Text));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check clipboard text format");
            return Task.FromResult(false);
        }
    }

    public Task<bool> HasImageAsync()
    {
        try
        {
            var content = Clipboard.GetContent();
            return Task.FromResult(content.Contains(StandardDataFormats.Bitmap));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check clipboard bitmap format");
            return Task.FromResult(false);
        }
    }

    public async Task<byte[]?> GetImageDataAsync()
    {
        try
        {
            var content = Clipboard.GetContent();
            if (content.Contains(StandardDataFormats.Bitmap))
            {
                var bitmapStreamRef = await content.GetBitmapAsync();
                using var streamWithContent = await bitmapStreamRef.OpenReadAsync();
                var buffer = new Windows.Storage.Streams.Buffer((uint)streamWithContent.Size);
                await streamWithContent.ReadAsync(buffer, (uint)streamWithContent.Size, InputStreamOptions.None);
                return buffer.ToArray();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get image bytes from clipboard");
        }

        return null;
    }

    public async Task SetImageDataAsync(byte[] imageBytes)
    {
        try
        {
            var memoryStream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(memoryStream))
            {
                writer.WriteBytes(imageBytes);
                await writer.StoreAsync();
                await writer.FlushAsync();
                writer.DetachStream();
            }
            memoryStream.Seek(0);

            var dataPackage = new DataPackage();
            dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromStream(memoryStream));
            Clipboard.SetContent(dataPackage);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set image to clipboard");
        }
    }

    public Task ClearAsync()
    {
        try
        {
            Clipboard.Clear();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear clipboard");
        }

        return Task.CompletedTask;
    }
}
