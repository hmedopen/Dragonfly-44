using Dragonfly44.Application.Common.TypeR;
using Dragonfly44.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Dragonfly44.Tests;

public class TypeRPrefixHandlerTests
{
    [Theory]
    [InlineData(BubbleShapeType.StandardSpeech, "REG:")]
    [InlineData(BubbleShapeType.Shout, "SHOUT:")]
    [InlineData(BubbleShapeType.Whisper, "WHIS:")]
    [InlineData(BubbleShapeType.Thought, "THO:")]
    [InlineData(BubbleShapeType.NarrativeBox, "NAR:")]
    [InlineData(BubbleShapeType.SoundEffect, "SFX:")]
    public void GetPrefixForShapeType_ShouldReturnCanonicalPrefix(BubbleShapeType shape, string expectedPrefix)
    {
        var prefix = TypeRPrefixHandler.GetPrefixForShapeType(shape);
        prefix.Should().Be(expectedPrefix);
    }

    [Theory]
    [InlineData("REG", BubbleShapeType.StandardSpeech)]
    [InlineData("REG:", BubbleShapeType.StandardSpeech)]
    [InlineData("[REG]", BubbleShapeType.StandardSpeech)]
    [InlineData("SHOUT", BubbleShapeType.Shout)]
    [InlineData("[SHOUT]:", BubbleShapeType.Shout)]
    [InlineData("WHIS", BubbleShapeType.Whisper)]
    [InlineData("WHISPER", BubbleShapeType.Whisper)]
    [InlineData("THO", BubbleShapeType.Thought)]
    [InlineData("THINK", BubbleShapeType.Thought)]
    [InlineData("NAR", BubbleShapeType.NarrativeBox)]
    [InlineData("NARRATION", BubbleShapeType.NarrativeBox)]
    [InlineData("SFX", BubbleShapeType.SoundEffect)]
    public void MapPrefixToShapeType_ShouldMapVariationsToCorrectEnum(string prefix, BubbleShapeType expectedShape)
    {
        var shape = TypeRPrefixHandler.MapPrefixToShapeType(prefix);
        shape.Should().Be(expectedShape);
    }

    [Fact]
    public void ParseLine_WithColonPrefix_ShouldExtractPrefixAndCleanText()
    {
        var line = "REG: Hello from the other side";
        var (hasPrefix, prefix, shape, cleanText) = TypeRPrefixHandler.ParseLine(line);

        hasPrefix.Should().BeTrue();
        prefix.Should().Be("REG:");
        shape.Should().Be(BubbleShapeType.StandardSpeech);
        cleanText.Should().Be("Hello from the other side");
    }

    [Fact]
    public void ParseLine_WithBracketPrefix_ShouldExtractPrefixAndCleanText()
    {
        var line = "[SFX]: *BOOOM*";
        var (hasPrefix, prefix, shape, cleanText) = TypeRPrefixHandler.ParseLine(line);

        hasPrefix.Should().BeTrue();
        prefix.Should().Be("SFX:");
        shape.Should().Be(BubbleShapeType.SoundEffect);
        cleanText.Should().Be("*BOOOM*");
    }

    [Fact]
    public void ParseLine_WithArabicTextAndPrefix_ShouldExtractPrefixCleanly()
    {
        var line = "NAR: في يوم من الأيام الجميلة";
        var (hasPrefix, prefix, shape, cleanText) = TypeRPrefixHandler.ParseLine(line);

        hasPrefix.Should().BeTrue();
        prefix.Should().Be("NAR:");
        shape.Should().Be(BubbleShapeType.NarrativeBox);
        cleanText.Should().Be("في يوم من الأيام الجميلة");
    }

    [Fact]
    public void ParseLine_WithoutPrefix_ShouldReturnOriginalText()
    {
        var line = "Just plain text without prefix";
        var (hasPrefix, prefix, shape, cleanText) = TypeRPrefixHandler.ParseLine(line);

        hasPrefix.Should().BeFalse();
        prefix.Should().BeEmpty();
        shape.Should().Be(BubbleShapeType.StandardSpeech);
        cleanText.Should().Be("Just plain text without prefix");
    }

    [Fact]
    public void FormatWithPrefix_ShouldReconstructTypeRLineCorrectly()
    {
        var formatted = TypeRPrefixHandler.FormatWithPrefix("أهلاً بالعالم", BubbleShapeType.Whisper);
        formatted.Should().Be("WHIS: أهلاً بالعالم");
    }

    [Fact]
    public void WrapWithLtrIsolation_ShouldWrapWithUnicodeIsolates()
    {
        var isolated = TypeRPrefixHandler.WrapWithLtrIsolation("REG:");
        isolated.Should().Be("\u2066REG:\u2069");
    }
}
