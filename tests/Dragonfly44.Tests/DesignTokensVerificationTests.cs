using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace Dragonfly44.Tests;

public class DesignTokensVerificationTests
{
    private readonly string _themesDirectory;

    public DesignTokensVerificationTests()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string? found = null;
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Dragonfly44.Presentation", "Themes");
            if (Directory.Exists(candidate))
            {
                found = candidate;
                break;
            }
            dir = dir.Parent;
        }

        _themesDirectory = found ?? throw new DirectoryNotFoundException("Could not locate Themes directory.");
    }

    [Fact]
    public void ColorsXaml_ShouldContainNeutralPlaceholderColorTokens()
    {
        var filePath = Path.Combine(_themesDirectory, "Tokens.Colors.xaml");
        File.Exists(filePath).Should().BeTrue("Tokens.Colors.xaml must exist");

        var doc = XDocument.Load(filePath);
        var keys = doc.Descendants().Attributes(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")).Select(a => a.Value).ToList();

        keys.Should().Contain("DragonflyPlaceholderWindowBgBrush");
        keys.Should().Contain("DragonflyPlaceholderSurfaceBrush");
        keys.Should().Contain("DragonflyPlaceholderBorderBrush");
        keys.Should().Contain("DragonflyPlaceholderTextPrimaryBrush");
        keys.Should().Contain("DragonflyPlaceholderAccentBrush");
        keys.Should().Contain("DragonflyPlaceholderSuccessBrush");
        keys.Should().Contain("DragonflyPlaceholderHoverBrush");
    }

    [Fact]
    public void DimensionsXaml_ShouldContainSpacingAndRadiusTokens()
    {
        var filePath = Path.Combine(_themesDirectory, "Tokens.Dimensions.xaml");
        File.Exists(filePath).Should().BeTrue("Tokens.Dimensions.xaml must exist");

        var doc = XDocument.Load(filePath);
        var keys = doc.Descendants().Attributes(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")).Select(a => a.Value).ToList();

        keys.Should().Contain("DragonflySpace8");
        keys.Should().Contain("DragonflySpace16");
        keys.Should().Contain("DragonflySpace24");
        keys.Should().Contain("DragonflyRadiusSmall");
        keys.Should().Contain("DragonflyRadiusMedium");
        keys.Should().Contain("DragonflyBorderThin");
        keys.Should().Contain("DragonflyTitleBarHeight");
    }

    [Fact]
    public void TypographyXaml_ShouldContainFontFamilyAndSizeTokens()
    {
        var filePath = Path.Combine(_themesDirectory, "Tokens.Typography.xaml");
        File.Exists(filePath).Should().BeTrue("Tokens.Typography.xaml must exist");

        var doc = XDocument.Load(filePath);
        var keys = doc.Descendants().Attributes(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")).Select(a => a.Value).ToList();

        keys.Should().Contain("DragonflyPlaceholderFontUi");
        keys.Should().Contain("DragonflyPlaceholderFontMonospace");
        keys.Should().Contain("DragonflyPlaceholderFontSizeBody");
        keys.Should().Contain("DragonflyPlaceholderFontSizeHeading");
    }

    [Fact]
    public void GenericXaml_ShouldDefineStylesForCustomComponents()
    {
        var filePath = Path.Combine(_themesDirectory, "Generic.xaml");
        File.Exists(filePath).Should().BeTrue("Generic.xaml must exist");

        var doc = XDocument.Load(filePath);
        var targetTypes = doc.Descendants().Attributes("TargetType").Select(a => a.Value).ToList();

        targetTypes.Should().Contain("windowing:CustomTitleBar");
        targetTypes.Should().Contain("controls:CustomButton");
        targetTypes.Should().Contain("controls:CustomDialogHost");
    }
}
