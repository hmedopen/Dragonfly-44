using Dragonfly44.Application.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.Graphics.Canvas;

namespace Dragonfly44.Presentation.Rendering;

/// <summary>
/// Hardware-accelerated LRU GPU texture cache for manga pages.
/// Ensures finite VRAM consumption by disposing evicted CanvasBitmap textures immediately.
/// </summary>
public class MangaImageCache : IDisposable
{
    private readonly LruResourceCache<string, CanvasBitmap> _cache;
    private readonly IMangaImageLoader _imageLoader;
    private readonly ILogger<MangaImageCache> _logger;
    private bool _isDisposed;

    public int MaxCapacity => _cache.Capacity;
    public int Count => _cache.Count;

    public MangaImageCache(
        IMangaImageLoader imageLoader,
        ILogger<MangaImageCache> logger,
        int maxCapacity = 6)
    {
        _imageLoader = imageLoader;
        _logger = logger;
        _cache = new LruResourceCache<string, CanvasBitmap>(maxCapacity);
        _cache.ItemEvicted += (s, evicted) =>
        {
            _logger.LogDebug("Evicted manga texture from GPU cache");
        };
    }

    public CanvasBitmap? TryGet(string key) => _cache.TryGet(key);

    public void Put(string key, CanvasBitmap bitmap) => _cache.Put(key, bitmap);

    public async Task<CanvasBitmap?> GetOrLoadAsync(
        ICanvasResourceCreator resourceCreator,
        string key,
        string absolutePath,
        CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrLoadAsync(
            key,
            async ct => await _imageLoader.LoadBitmapAsync(resourceCreator, absolutePath, ct),
            cancellationToken);
    }

    public void CancelLoad(string key) => _cache.CancelLoad(key);

    public void Clear() => _cache.Clear();

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            _cache.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
