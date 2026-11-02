using System.Numerics;
using Dragonfly44.Application.Rendering;
using FluentAssertions;
using Xunit;

namespace Dragonfly44.Tests;

public class CanvasTransformTests
{
    [Fact]
    public void Zoom_ShouldBeClampedBetweenMinAndMax()
    {
        var transform = new CanvasTransform();

        transform.Zoom = 0.05f;
        transform.Zoom.Should().Be(CanvasTransform.DefaultMinZoom);

        transform.Zoom = 15.0f;
        transform.Zoom.Should().Be(CanvasTransform.DefaultMaxZoom);

        transform.Zoom = 2.5f;
        transform.Zoom.Should().Be(2.5f);
    }

    [Fact]
    public void ScreenToWorldAndWorldToScreen_ShouldBeExactInverses()
    {
        var transform = new CanvasTransform(1.75f, new Vector2(120f, -45f));
        var originalWorldPoint = new Vector2(350f, 620f);

        var screenPoint = transform.WorldToScreen(originalWorldPoint);
        var recoveredWorldPoint = transform.ScreenToWorld(screenPoint);

        recoveredWorldPoint.X.Should().BeApproximately(originalWorldPoint.X, 0.001f);
        recoveredWorldPoint.Y.Should().BeApproximately(originalWorldPoint.Y, 0.001f);
    }

    [Fact]
    public void ZoomAt_ShouldKeepWorldCoordinateUnderCursorStationary()
    {
        var transform = new CanvasTransform(1.0f, new Vector2(50f, 50f));
        var cursorScreen = new Vector2(400f, 300f);

        var worldBeforeZoom = transform.ScreenToWorld(cursorScreen);

        transform.ZoomAt(cursorScreen, 2.5f);

        var worldAfterZoom = transform.ScreenToWorld(cursorScreen);

        worldAfterZoom.X.Should().BeApproximately(worldBeforeZoom.X, 0.001f);
        worldAfterZoom.Y.Should().BeApproximately(worldBeforeZoom.Y, 0.001f);
    }

    [Theory]
    [InlineData(1000, 800, 1200, 1800)] // Tall manga page
    [InlineData(1920, 1080, 800, 1200)]  // Wide screen, standard page
    [InlineData(800, 1200, 2400, 1600)]  // Double-page spread
    public void CalculateFitPage_ShouldFitPageWithinViewportWithoutStretching(
        float vpWidth, float vpHeight, float pageWidth, float pageHeight)
    {
        var (fitZoom, pan) = CanvasTransform.CalculateFitPage(vpWidth, vpHeight, pageWidth, pageHeight, padding: 20f);

        var renderedW = pageWidth * fitZoom;
        var renderedH = pageHeight * fitZoom;

        // Rendered dimensions must fit within viewport minus padding
        renderedW.Should().BeLessThanOrEqualTo(vpWidth);
        renderedH.Should().BeLessThanOrEqualTo(vpHeight);

        // Aspect ratio must be strictly preserved
        var originalAspectRatio = pageWidth / pageHeight;
        var renderedAspectRatio = renderedW / renderedH;
        renderedAspectRatio.Should().BeApproximately(originalAspectRatio, 0.0001f);

        // Must be centered
        pan.X.Should().BeApproximately((vpWidth - renderedW) * 0.5f, 0.01f);
        pan.Y.Should().BeApproximately((vpHeight - renderedH) * 0.5f, 0.01f);
    }

    [Fact]
    public void CalculateFitWidth_ShouldScalePageToMatchViewportWidth()
    {
        float vpWidth = 1200f;
        float vpHeight = 900f;
        float pageWidth = 800f;
        float pageHeight = 1200f;
        float padding = 20f;

        var (fitZoom, pan) = CanvasTransform.CalculateFitWidth(vpWidth, vpHeight, pageWidth, pageHeight, padding);

        var renderedW = pageWidth * fitZoom;
        renderedW.Should().BeApproximately(vpWidth - (padding * 2f), 0.01f);

        // Aspect ratio must be preserved
        var renderedH = pageHeight * fitZoom;
        (renderedW / renderedH).Should().BeApproximately(pageWidth / pageHeight, 0.0001f);
    }

    [Fact]
    public void Reset_ShouldResetZoomTo100PercentAndCenter()
    {
        var transform = new CanvasTransform(3.5f, new Vector2(400f, -200f));
        transform.Reset(1000f, 800f, 600f, 900f);

        transform.Zoom.Should().Be(1.0f);
        transform.Pan.X.Should().Be((1000f - 600f) * 0.5f);
        transform.Pan.Y.Should().Be((800f - 900f) * 0.5f);
    }
}
