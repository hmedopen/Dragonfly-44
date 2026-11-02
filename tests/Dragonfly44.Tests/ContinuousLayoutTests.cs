using Dragonfly44.Application.Rendering;
using FluentAssertions;
using Xunit;

namespace Dragonfly44.Tests;

public class ContinuousLayoutTests
{
    private List<MangaPageLayoutInfo> CreateSampleChapter(int count = 10, float pageHeight = 1500f)
    {
        var list = new List<MangaPageLayoutInfo>();
        for (int i = 1; i <= count; i++)
        {
            list.Add(new MangaPageLayoutInfo(
                Guid.NewGuid(),
                i,
                $"page_{i:D3}.png",
                $"projects/p1/ch1/pages/page_{i:D3}.png",
                "PNG",
                1000f,
                pageHeight));
        }
        return list;
    }

    [Fact]
    public void LayoutPages_ShouldStackPagesSequentiallyWithConfiguredGap()
    {
        var calculator = new ContinuousLayoutCalculator { PageGap = 30f };
        var pages = CreateSampleChapter(5, pageHeight: 1000f);

        var totalHeight = calculator.LayoutPages(pages);

        pages[0].OffsetY.Should().Be(0f);
        pages[1].OffsetY.Should().Be(1030f);
        pages[2].OffsetY.Should().Be(2060f);
        pages[3].OffsetY.Should().Be(3090f);
        pages[4].OffsetY.Should().Be(4120f);

        // Total height: 5 pages * 1000 + 4 gaps * 30 = 5120
        totalHeight.Should().Be(5120f);
    }

    [Fact]
    public void GetVisiblePageIndices_ShouldOnlyReturnPagesIntersectingViewport()
    {
        var calculator = new ContinuousLayoutCalculator { PageGap = 20f };
        var pages = CreateSampleChapter(10, pageHeight: 1000f);
        calculator.LayoutPages(pages);

        // Viewport looking at Y = 2500 to Y = 3500 (with 0 buffer)
        // Page 2: 2040 to 3040 -> Intersects!
        // Page 3: 3060 to 4060 -> Intersects!
        // Page 0 (0-1000), Page 1 (1020-2020), Page 4 (4080-5080) -> Excluded!
        var visible = calculator.GetVisiblePageIndices(pages, 2500f, 3500f, bufferPixels: 0f);

        visible.Should().Equal(new[] { 2, 3 });
    }

    [Fact]
    public void GetVisiblePageIndices_WithBuffer_ShouldIncludeAdjacentPrefetchPages()
    {
        var calculator = new ContinuousLayoutCalculator { PageGap = 20f };
        var pages = CreateSampleChapter(10, pageHeight: 1000f);
        calculator.LayoutPages(pages);

        // Viewport looking at Y = 2500 to Y = 3500 with 500px buffer (queries 2000 to 4000)
        // Page 1 ends at 2020 -> Intersects!
        // Page 2: 2040-3040 -> Intersects!
        // Page 3: 3060-4060 -> Intersects!
        var visible = calculator.GetVisiblePageIndices(pages, 2500f, 3500f, bufferPixels: 500f);

        visible.Should().Contain(new[] { 1, 2, 3 });
    }

    [Fact]
    public void GetPrimaryPageIndexAt_ShouldIdentifyPageAtViewportCenter()
    {
        var calculator = new ContinuousLayoutCalculator { PageGap = 20f };
        var pages = CreateSampleChapter(10, pageHeight: 1000f);
        calculator.LayoutPages(pages);

        // Center at Y = 500 -> Page 0 (0-1000)
        calculator.GetPrimaryPageIndexAt(pages, 500f).Should().Be(0);

        // Center at Y = 2500 -> Page 2 (2040-3040)
        calculator.GetPrimaryPageIndexAt(pages, 2500f).Should().Be(2);

        // Center at Y = 8500 -> Page 8 (8160-9160)
        calculator.GetPrimaryPageIndexAt(pages, 8500f).Should().Be(8);
    }

    [Fact]
    public void GetPageOffset_ShouldReturnExactWorldOffsetYForGivenPage()
    {
        var calculator = new ContinuousLayoutCalculator { PageGap = 20f };
        var pages = CreateSampleChapter(6, pageHeight: 800f);
        calculator.LayoutPages(pages);

        calculator.GetPageOffset(pages, 0).Should().Be(0f);
        calculator.GetPageOffset(pages, 3).Should().Be(3 * 800f + 3 * 20f); // 2460f
    }
}
