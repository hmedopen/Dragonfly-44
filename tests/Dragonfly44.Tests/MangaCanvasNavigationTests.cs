using Dragonfly44.Application.Rendering;
using FluentAssertions;
using Xunit;

namespace Dragonfly44.Tests;

public class MangaCanvasNavigationTests
{
    private List<MangaPageLayoutInfo> CreateSamplePages(int count = 35)
    {
        var list = new List<MangaPageLayoutInfo>();
        for (int i = 1; i <= count; i++)
        {
            list.Add(new MangaPageLayoutInfo(
                Guid.NewGuid(),
                i,
                $"page_{i:D2}.png",
                $"pages/page_{i:D2}.png",
                "PNG",
                1400f,
                2100f));
        }
        return list;
    }

    [Fact]
    public void LoadPages_ShouldInitializeWithFirstPageAndCorrectPageCount()
    {
        var session = new CanvasNavigationSession();
        var pages = CreateSamplePages(35);

        session.LoadPages(pages, "Chapter 1: The Beginning");

        session.TotalPages.Should().Be(35);
        session.CurrentPageIndex.Should().Be(0);
        session.CurrentPageNumber.Should().Be(1);
        session.CanGoPrevious.Should().BeFalse();
        session.CanGoNext.Should().BeTrue();
        session.PageIndicatorText.Should().Be("Page 1 of 35");
    }

    [Fact]
    public void NextPageAndPreviousPage_ShouldAdvanceAndRespectBounds()
    {
        var session = new CanvasNavigationSession();
        var pages = CreateSamplePages(3);
        session.LoadPages(pages, "Sample Chapter");

        // Page 1 -> 2
        session.NextPage();
        session.CurrentPageIndex.Should().Be(1);
        session.CanGoPrevious.Should().BeTrue();
        session.CanGoNext.Should().BeTrue();
        session.PageIndicatorText.Should().Be("Page 2 of 3");

        // Page 2 -> 3
        session.NextPage();
        session.CurrentPageIndex.Should().Be(2);
        session.CanGoPrevious.Should().BeTrue();
        session.CanGoNext.Should().BeFalse();
        session.PageIndicatorText.Should().Be("Page 3 of 3");

        // Attempt next beyond bounds -> stays at page 3
        session.NextPage();
        session.CurrentPageIndex.Should().Be(2);

        // Previous back to page 2
        session.PreviousPage();
        session.CurrentPageIndex.Should().Be(1);
    }

    [Fact]
    public void SetPage_ShouldJumpToTargetPage()
    {
        var session = new CanvasNavigationSession();
        var pages = CreateSamplePages(40);
        session.LoadPages(pages, "Large Chapter");

        session.SetPage(19); // 0-based index for Page 20
        session.CurrentPageIndex.Should().Be(19);
        session.CurrentPageNumber.Should().Be(20);
        session.PageIndicatorText.Should().Be("Page 20 of 40");
    }

    [Fact]
    public void ToggleViewMode_ShouldPreserveReadingPosition()
    {
        var session = new CanvasNavigationSession();
        var pages = CreateSamplePages(30);
        session.LoadPages(pages, "Mode Switch Chapter");

        session.SetPage(14); // Page 15
        session.Mode.Should().Be(CanvasViewMode.Page);

        // Toggle to Continuous
        session.ToggleViewMode();
        session.Mode.Should().Be(CanvasViewMode.Continuous);
        session.CurrentPageIndex.Should().Be(14);

        // Toggle back to Page
        session.ToggleViewMode();
        session.Mode.Should().Be(CanvasViewMode.Page);
        session.CurrentPageIndex.Should().Be(14);
    }

    [Fact]
    public void ZoomCommands_ShouldScaleZoomLevel()
    {
        var session = new CanvasNavigationSession();
        var pages = CreateSamplePages(5);
        session.LoadPages(pages, "Zoom Test");

        session.Transform.Zoom = 1.0f;

        session.ZoomIn(0.2f);
        session.Transform.Zoom.Should().Be(1.2f);

        session.ZoomOut(0.4f);
        session.Transform.Zoom.Should().BeApproximately(0.8f, 0.001f);
    }

    [Fact]
    public void ChapterNavigation_With40Pages_ShouldCalculateContinuousLayoutCorrectly()
    {
        var session = new CanvasNavigationSession();
        var pages = CreateSamplePages(40);
        session.LoadPages(pages, "High Volume Chapter");
        session.Mode = CanvasViewMode.Continuous;

        session.Pages.Count.Should().Be(40);
        // Last page offset should be at 39 * (2100 + gap)
        var expectedLastOffsetY = 39 * (2100f + session.LayoutCalculator.PageGap);
        session.Pages[39].OffsetY.Should().Be(expectedLastOffsetY);
    }
}
