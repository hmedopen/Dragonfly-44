using System.Globalization;
using Dragonfly44.Application.Common.TypeR;
using Dragonfly44.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Dragonfly44.Tests;

public class ArabicTextHandlingTests
{
    [Fact]
    public void ArabicUnicodeCharacters_ShouldBePreservedIntact()
    {
        var arabicText = "هذا نص عربي تجريبي لاختبار واجهة المستخدم وترجمة المانجا";
        arabicText.Should().NotBeNullOrEmpty();
        arabicText.Length.Should().Be(56);

        // Verify Arabic character category
        foreach (var ch in arabicText)
        {
            if (char.IsWhiteSpace(ch)) continue;
            var cat = char.GetUnicodeCategory(ch);
            (cat == UnicodeCategory.OtherLetter || cat == UnicodeCategory.NonSpacingMark).Should().BeTrue();
        }
    }

    [Fact]
    public void ArabicDiacriticsAndTashkeel_ShouldBePreservedAccurately()
    {
        // Text with Fathah, Dammah, Kasrah, Sukun, Shaddah, and Tanween
        var vocalizedArabic = "بِسْمِ اللَّهِ الرَّحْمَٰنِ الرَّحِيمِ - قُوَّةٌ هَائِلَةٌ";

        vocalizedArabic.Should().Contain("َّ"); // Shaddah + Fathah
        vocalizedArabic.Should().Contain("ٌ"); // Tanween Damm
        vocalizedArabic.Should().Contain("ِ"); // Kasrah
        vocalizedArabic.Should().Contain("ْ"); // Sukun

        // Roundtrip format through TypeR handler
        var formatted = TypeRPrefixHandler.FormatWithPrefix(vocalizedArabic, BubbleShapeType.Shout);
        formatted.Should().StartWith("SHOUT: ");
        formatted.Should().Contain(vocalizedArabic);

        var parsed = TypeRPrefixHandler.ParseLine(formatted);
        parsed.CleanText.Should().Be(vocalizedArabic);
    }

    [Fact]
    public void ArabicPunctuationMarks_ShouldBePreservedWithoutCorruptingPositions()
    {
        var punctuatedText = "«هل أنت متأكد من ذلك؟» قال: «نعم، بالتأكيد؛ لا شك في هذا!»";

        punctuatedText.Should().Contain("؟"); // Arabic question mark
        punctuatedText.Should().Contain("،"); // Arabic comma
        punctuatedText.Should().Contain("؛"); // Arabic semicolon
        punctuatedText.Should().Contain("«"); // Opening guillemet
        punctuatedText.Should().Contain("»"); // Closing guillemet

        var parsed = TypeRPrefixHandler.ParseLine("REG: " + punctuatedText);
        parsed.HasPrefix.Should().BeTrue();
        parsed.CleanText.Should().Be(punctuatedText);
    }

    [Fact]
    public void MixedArabicAndLatinText_ShouldMaintainBothScriptsIntact()
    {
        var mixedText = "سلاح Dragonslayer قوي جداً ضد وحوش الـ Apostles في مانجا Berserk.";

        mixedText.Should().Contain("Dragonslayer");
        mixedText.Should().Contain("Apostles");
        mixedText.Should().Contain("Berserk");
        mixedText.Should().Contain("سلاح");
        mixedText.Should().Contain("قوي جداً");

        // Format with TypeR prefix
        var formatted = TypeRPrefixHandler.FormatWithPrefix(mixedText, BubbleShapeType.NarrativeBox);
        var parsed = TypeRPrefixHandler.ParseLine(formatted);

        parsed.ShapeType.Should().Be(BubbleShapeType.NarrativeBox);
        parsed.CleanText.Should().Be(mixedText);
    }

    [Fact]
    public void TatweelAndKashida_ShouldBePreservedInText()
    {
        // Tatweel / Kashida (U+0640)
        var elongatedText = "مـــــرحـــــبـــــاً";
        elongatedText.Should().Contain("\u0640");

        var parsed = TypeRPrefixHandler.ParseLine("WHIS: " + elongatedText);
        parsed.CleanText.Should().Be(elongatedText);
    }
}
