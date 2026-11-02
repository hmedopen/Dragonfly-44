using Dragonfly44.Application.Rendering;
using FluentAssertions;
using Xunit;

namespace Dragonfly44.Tests;

public class ImageCacheAndResourceTests
{
    private class MockGpuTexture : IDisposable
    {
        public string Name { get; }
        public bool IsDisposed { get; private set; }

        public MockGpuTexture(string name)
        {
            Name = name;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    [Fact]
    public void Put_WhenCapacityExceeded_ShouldEvictAndDisposeOldestItem()
    {
        using var cache = new LruResourceCache<string, MockGpuTexture>(capacity: 3);

        var tex1 = new MockGpuTexture("Page1");
        var tex2 = new MockGpuTexture("Page2");
        var tex3 = new MockGpuTexture("Page3");
        var tex4 = new MockGpuTexture("Page4");

        cache.Put("p1", tex1);
        cache.Put("p2", tex2);
        cache.Put("p3", tex3);

        cache.Count.Should().Be(3);
        tex1.IsDisposed.Should().BeFalse();

        // Adding 4th item should evict p1 (oldest)
        cache.Put("p4", tex4);

        cache.Count.Should().Be(3);
        tex1.IsDisposed.Should().BeTrue("Evicted resource must be immediately disposed to free GPU memory");
        cache.TryGet("p1").Should().BeNull();
        cache.TryGet("p4").Should().BeSameAs(tex4);
    }

    [Fact]
    public void TryGet_ShouldPromoteItemToMRU_PreventingEarlyEviction()
    {
        using var cache = new LruResourceCache<string, MockGpuTexture>(capacity: 3);

        var tex1 = new MockGpuTexture("Page1");
        var tex2 = new MockGpuTexture("Page2");
        var tex3 = new MockGpuTexture("Page3");
        var tex4 = new MockGpuTexture("Page4");

        cache.Put("p1", tex1);
        cache.Put("p2", tex2);
        cache.Put("p3", tex3);

        // Access p1 so it becomes most recently used
        cache.TryGet("p1").Should().BeSameAs(tex1);

        // Adding p4 should now evict p2 instead of p1!
        cache.Put("p4", tex4);

        tex1.IsDisposed.Should().BeFalse("Accessed item should have been promoted to MRU");
        tex2.IsDisposed.Should().BeTrue("Least recently accessed item p2 should be evicted and disposed");
        cache.TryGet("p1").Should().BeSameAs(tex1);
        cache.TryGet("p2").Should().BeNull();
    }

    [Fact]
    public async Task GetOrLoadAsync_WithCancellation_ShouldCancelInFlightOperation()
    {
        using var cache = new LruResourceCache<string, MockGpuTexture>(capacity: 3);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancelled

        var factoryExecuted = false;
        var result = await cache.GetOrLoadAsync("p1", async token =>
        {
            factoryExecuted = true;
            token.ThrowIfCancellationRequested();
            await Task.Yield();
            return new MockGpuTexture("Page1");
        }, cts.Token);

        result.Should().BeNull();
        factoryExecuted.Should().BeTrue();
        cache.TryGet("p1").Should().BeNull();
    }

    [Fact]
    public void Clear_ShouldDisposeAllCachedTextures()
    {
        using var cache = new LruResourceCache<string, MockGpuTexture>(capacity: 3);

        var tex1 = new MockGpuTexture("Page1");
        var tex2 = new MockGpuTexture("Page2");

        cache.Put("p1", tex1);
        cache.Put("p2", tex2);

        cache.Clear();

        cache.Count.Should().Be(0);
        tex1.IsDisposed.Should().BeTrue();
        tex2.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void Dispose_ShouldDisposeAllItemsAndRejectFuturePuts()
    {
        var cache = new LruResourceCache<string, MockGpuTexture>(capacity: 3);
        var tex1 = new MockGpuTexture("Page1");
        cache.Put("p1", tex1);

        cache.Dispose();

        tex1.IsDisposed.Should().BeTrue();

        var tex2 = new MockGpuTexture("Page2");
        cache.Put("p2", tex2);
        tex2.IsDisposed.Should().BeTrue("Adding to disposed cache should immediately dispose the resource");
    }
}
