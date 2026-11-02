using Dragonfly44.Application.Common;
using FluentAssertions;
using Xunit;

namespace Dragonfly44.Tests;

public class NaturalStringComparerTests
{
    private readonly NaturalStringComparer _comparer = new();

    [Fact]
    public void NaturalSort_ShouldOrderNumberedPagesCorrectly()
    {
        // Arrange - requirement: page1.png, page2.png, page3.png, page10.png, page11.png
        var input = new List<string>
        {
            "page11.png",
            "page2.png",
            "page10.png",
            "page1.png",
            "page3.png"
        };

        // Act
        var sorted = input.OrderBy(x => x, _comparer).ToList();

        // Assert
        sorted.Should().ContainInOrder(
            "page1.png",
            "page2.png",
            "page3.png",
            "page10.png",
            "page11.png"
        );
    }

    [Fact]
    public void NaturalSort_ShouldHandleLeadingZeros()
    {
        var input = new List<string>
        {
            "002.jpg",
            "010.jpg",
            "001.jpg",
            "020.jpg",
            "003.jpg"
        };

        var sorted = input.OrderBy(x => x, _comparer).ToList();

        sorted.Should().ContainInOrder(
            "001.jpg",
            "002.jpg",
            "003.jpg",
            "010.jpg",
            "020.jpg"
        );
    }

    [Fact]
    public void NaturalSort_ShouldBeCaseInsensitive()
    {
        var input = new List<string>
        {
            "PAGE10.png",
            "page2.png",
            "Page1.png"
        };

        var sorted = input.OrderBy(x => x, _comparer).ToList();

        sorted.Should().ContainInOrder(
            "Page1.png",
            "page2.png",
            "PAGE10.png"
        );
    }

    [Fact]
    public void NaturalSort_ShouldHandleMixedAlphanumericChunks()
    {
        var input = new List<string>
        {
            "chapter_2_part_10.png",
            "chapter_1_part_1.png",
            "chapter_1_part_2.png",
            "chapter_2_part_2.png",
            "chapter_1_part_10.png"
        };

        var sorted = input.OrderBy(x => x, _comparer).ToList();

        sorted.Should().ContainInOrder(
            "chapter_1_part_1.png",
            "chapter_1_part_2.png",
            "chapter_1_part_10.png",
            "chapter_2_part_2.png",
            "chapter_2_part_10.png"
        );
    }

    [Fact]
    public void Compare_ShouldHandleNullsSafely()
    {
        _comparer.Compare(null, null).Should().Be(0);
        _comparer.Compare(null, "page1.png").Should().BeNegative();
        _comparer.Compare("page1.png", null).Should().BePositive();
    }
}
