using System.Text.RegularExpressions;
using Dragonfly44.Domain.Enums;

namespace Dragonfly44.Application.Common.TypeR;

/// <summary>
/// Handles TypeR typesetting prefix syntax, extraction, shape type mapping, and BiDi isolation.
/// </summary>
public static class TypeRPrefixHandler
{
    // Canonical TypeR Prefixes
    public const string PrefixRegular = "REG:";
    public const string PrefixShout = "SHOUT:";
    public const string PrefixWhisper = "WHIS:";
    public const string PrefixThought = "THO:";
    public const string PrefixNarration = "NAR:";
    public const string PrefixSoundEffect = "SFX:";
    public const string PrefixAlternative = "ALT:";

    // Unicode Bidirectional Isolation Markers
    public const char LtrIsolate = '\u2066';
    public const char PopDirectionalIsolate = '\u2069';

    // Regex matching prefixes at the beginning of a line (e.g. "REG:", "REG: ", "[REG]", "[SFX]:")
    private static readonly Regex PrefixRegex = new(
        @"^(?:\[(?<tag>[A-Za-z0-9_]+)\]:?|(?<tag>[A-Za-z0-9_]+):)\s*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Gets the standard TypeR prefix for a given bubble shape classification.
    /// </summary>
    public static string GetPrefixForShapeType(BubbleShapeType shapeType) => shapeType switch
    {
        BubbleShapeType.StandardSpeech => PrefixRegular,
        BubbleShapeType.Shout => PrefixShout,
        BubbleShapeType.Whisper => PrefixWhisper,
        BubbleShapeType.Thought => PrefixThought,
        BubbleShapeType.NarrativeBox => PrefixNarration,
        BubbleShapeType.SoundEffect => PrefixSoundEffect,
        _ => PrefixRegular
    };

    /// <summary>
    /// Maps a prefix string (e.g. "REG", "REG:", "[SFX]") to the matching BubbleShapeType.
    /// </summary>
    public static BubbleShapeType MapPrefixToShapeType(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix)) return BubbleShapeType.StandardSpeech;

        var clean = prefix.Trim().Trim('[', ']', ':').ToUpperInvariant();
        return clean switch
        {
            "REG" or "REGULAR" or "SPEECH" => BubbleShapeType.StandardSpeech,
            "SHOUT" or "YELL" => BubbleShapeType.Shout,
            "WHIS" or "WHISPER" => BubbleShapeType.Whisper,
            "THO" or "THOUGHT" or "THINK" => BubbleShapeType.Thought,
            "NAR" or "NARR" or "NARRATION" or "BOX" => BubbleShapeType.NarrativeBox,
            "SFX" or "SOUND" or "EFFECT" => BubbleShapeType.SoundEffect,
            _ => BubbleShapeType.StandardSpeech
        };
    }

    /// <summary>
    /// Parses an input line to extract the TypeR prefix and clean content.
    /// </summary>
    public static (bool HasPrefix, string Prefix, BubbleShapeType ShapeType, string CleanText) ParseLine(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return (false, string.Empty, BubbleShapeType.StandardSpeech, string.Empty);
        }

        var match = PrefixRegex.Match(input);
        if (match.Success)
        {
            var rawTag = match.Groups["tag"].Value;
            var shapeType = MapPrefixToShapeType(rawTag);
            var canonicalPrefix = GetPrefixForShapeType(shapeType);
            var cleanText = input.Substring(match.Length);
            return (true, canonicalPrefix, shapeType, cleanText);
        }

        return (false, string.Empty, BubbleShapeType.StandardSpeech, input);
    }

    /// <summary>
    /// Formats a clean dialogue text with its canonical TypeR prefix.
    /// </summary>
    public static string FormatWithPrefix(string cleanText, BubbleShapeType shapeType)
    {
        var prefix = GetPrefixForShapeType(shapeType);
        return string.IsNullOrWhiteSpace(cleanText)
            ? prefix
            : $"{prefix} {cleanText.Trim()}";
    }

    /// <summary>
    /// Wraps a Latin TypeR prefix with Unicode Left-to-Right Isolates to protect surrounding RTL Arabic text.
    /// </summary>
    public static string WrapWithLtrIsolation(string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return string.Empty;
        return $"{LtrIsolate}{prefix}{PopDirectionalIsolate}";
    }
}
