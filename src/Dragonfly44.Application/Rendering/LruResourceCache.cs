using System.Collections.Concurrent;

namespace Dragonfly44.Application.Rendering;

/// <summary>
/// Thread-safe generic LRU resource cache with automatic disposal of evicted resources.
/// Guarantees that resource consumption is bounded and GPU/native resources are released immediately.
/// </summary>
public class LruResourceCache<TKey, TResource> : IDisposable
    where TKey : notnull
    where TResource : class, IDisposable
{
    private readonly int _capacity;
    private readonly object _lock = new();
    private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _map;
    private readonly LinkedList<CacheItem> _lruList = new();
    private readonly ConcurrentDictionary<TKey, CancellationTokenSource> _inFlightOperations = new();
    private bool _isDisposed;

    private class CacheItem
    {
        public TKey Key { get; }
        public TResource Resource { get; }

        public CacheItem(TKey key, TResource resource)
        {
            Key = key;
            Resource = resource;
        }
    }

    public int Capacity => _capacity;
    public int Count
    {
        get
        {
            lock (_lock) return _map.Count;
        }
    }

    public event EventHandler<TResource>? ItemEvicted;

    public LruResourceCache(int capacity = 6)
    {
        _capacity = Math.Max(1, capacity);
        _map = new Dictionary<TKey, LinkedListNode<CacheItem>>(_capacity);
    }

    /// <summary>
    /// Attempts to retrieve an item from the cache. Updates its position to MRU if found.
    /// </summary>
    public TResource? TryGet(TKey key)
    {
        lock (_lock)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                return node.Value.Resource;
            }
        }
        return null;
    }

    /// <summary>
    /// Adds or updates an item in the cache. If capacity is exceeded, evicts and disposes the LRU item.
    /// </summary>
    public void Put(TKey key, TResource resource)
    {
        TResource? toDispose = null;

        lock (_lock)
        {
            if (_isDisposed)
            {
                resource.Dispose();
                return;
            }

            if (_map.TryGetValue(key, out var existingNode))
            {
                toDispose = existingNode.Value.Resource;
                _lruList.Remove(existingNode);
                _map.Remove(key);
            }
            else if (_map.Count >= _capacity)
            {
                var oldest = _lruList.Last;
                if (oldest != null)
                {
                    _lruList.RemoveLast();
                    _map.Remove(oldest.Value.Key);
                    toDispose = oldest.Value.Resource;
                    ItemEvicted?.Invoke(this, toDispose);
                }
            }

            var item = new CacheItem(key, resource);
            var node = _lruList.AddFirst(item);
            _map[key] = node;
        }

        toDispose?.Dispose();
    }

    /// <summary>
    /// Gets or loads an item asynchronously with cancellation support.
    /// </summary>
    public async Task<TResource?> GetOrLoadAsync(
        TKey key,
        Func<CancellationToken, Task<TResource?>> factory,
        CancellationToken cancellationToken = default)
    {
        var existing = TryGet(key);
        if (existing != null) return existing;

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _inFlightOperations[key] = cts;

        try
        {
            var loaded = await factory(cts.Token);
            if (loaded != null)
            {
                Put(key, loaded);
            }
            return loaded;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            _inFlightOperations.TryRemove(key, out _);
            cts.Dispose();
        }
    }

    /// <summary>
    /// Cancels any in-flight load request for the specified key.
    /// </summary>
    public void CancelLoad(TKey key)
    {
        if (_inFlightOperations.TryRemove(key, out var cts))
        {
            try { cts.Cancel(); } catch { }
            cts.Dispose();
        }
    }

    /// <summary>
    /// Clears and disposes all cached resources.
    /// </summary>
    public void Clear()
    {
        foreach (var kvp in _inFlightOperations)
        {
            try { kvp.Value.Cancel(); kvp.Value.Dispose(); } catch { }
        }
        _inFlightOperations.Clear();

        List<TResource> disposables = new();

        lock (_lock)
        {
            foreach (var node in _map.Values)
            {
                disposables.Add(node.Value.Resource);
            }
            _map.Clear();
            _lruList.Clear();
        }

        foreach (var d in disposables)
        {
            d.Dispose();
        }
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            _isDisposed = true;
            Clear();
            GC.SuppressFinalize(this);
        }
    }
}
