namespace Dragonfly44.Application.Interfaces;

public interface IClipboardService
{
    Task<string?> GetTextAsync();
    Task SetTextAsync(string text);
    Task<bool> HasTextAsync();
    Task<bool> HasImageAsync();
    Task<byte[]?> GetImageDataAsync();
    Task SetImageDataAsync(byte[] imageBytes);
    Task ClearAsync();
}
