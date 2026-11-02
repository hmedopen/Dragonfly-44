using Dragonfly44.Application.Rendering;
using FluentAssertions;
using Xunit;

namespace Dragonfly44.Tests;

public class OverlayGeometryTests
{
    [Fact]
    public void NormalizedRect_ShouldClampCoordinatesBetweenZeroAndOne()
    {
        var rect = new NormalizedRect(-0.2f, 1.5f, 1.4f, 0.8f);

        rect.X.Should().Be(0.0f);
        rect.Y.Should().Be(1.0f);
        rect.Width.Should().Be(1.0f);
        rect.Height.Should().Be(0.0f); // 1.0 - Y = 0
    }

    [Fact]
    public void ToPixelsAndFromPixels_ShouldRoundtripAccurately()
    {
        float pageWidth = 1440f;
        float pageHeight = 2160f;

        float originalX = 240f;
        float originalY = 360f;
        float originalW = 480f;
        float originalH = 300f;

        var normalized = NormalizedRect.FromPixels(originalX, originalY, originalW, originalH, pageWidth, pageHeight);
        var (px, py, pw, ph) = normalized.ToPixels(pageWidth, pageHeight);

        px.Should().BeApproximately(originalX, 0.01f);
        py.Should().BeApproximately(originalY, 0.01f);
        pw.Should().BeApproximately(originalW, 0.01f);
        ph.Should().BeApproximately(originalH, 0.01f);
    }

    [Fact]
    public void NormalizedCoordinates_ShouldScaleIdenticallyAcrossResolutions()
    {
        // Dialogue bubble located at 20% X, 30% Y, 40% width, 25% height
        var normalized = new NormalizedRect(0.2f, 0.3f, 0.4f, 0.25f);

        // Standard 720p resolution
        var (px1, py1, pw1, ph1) = normalized.ToPixels(1000f, 1500f);
        px1.Should().BeApproximately(200f, 0.001f);
        py1.Should().BeApproximately(450f, 0.001f);
        pw1.Should().BeApproximately(400f, 0.001f);
        ph1.Should().BeApproximately(375f, 0.001f);

        // High resolution 4K scan (doubled)
        var (px2, py2, pw2, ph2) = normalized.ToPixels(2000f, 3000f);
        px2.Should().BeApproximately(px1 * 2f, 0.001f);
        py2.Should().BeApproximately(py1 * 2f, 0.001f);
        pw2.Should().BeApproximately(pw1 * 2f, 0.001f);
        ph2.Should().BeApproximately(ph1 * 2f, 0.001f);
    }

    [Fact]
    public void PageOverlayItem_ShouldSupportReadingOrderAndSelection()
    {
        var item = new PageOverlayItem
        {
            PageNumber = 1,
            Bounds = new NormalizedRect(0.1f, 0.2f, 0.3f, 0.4f),
            ReadingOrder = 3,
            IsSelected = true,
            Label = "Dialogue 1"
        };

        item.PageNumber.Should().Be(1);
        item.ReadingOrder.Should().Be(3);
        item.IsSelected.Should().BeTrue();
        item.Label.Should().Be("Dialogue 1");
    }
}
