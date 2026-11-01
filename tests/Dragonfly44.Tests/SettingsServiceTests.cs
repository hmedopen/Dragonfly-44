using Dragonfly44.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dragonfly44.Tests;

public class SettingsServiceTests
{
    private readonly SettingsService _service;

    public SettingsServiceTests()
    {
        _service = new SettingsService(NullLogger<SettingsService>.Instance);
    }

    [Fact]
    public void Get_NonExistentKey_ShouldReturnDefaultValue()
    {
        var result = _service.Get("NonExistentKey", "DefaultValue");
        result.Should().Be("DefaultValue");
    }

    [Fact]
    public void SetAndGet_PrimitiveTypes_ShouldStoreAndRetrieveCorrectly()
    {
        _service.Set("Test.Int", 42);
        _service.Set("Test.String", "Dragonfly44");
        _service.Set("Test.Bool", true);

        _service.Get("Test.Int", 0).Should().Be(42);
        _service.Get("Test.String", string.Empty).Should().Be("Dragonfly44");
        _service.Get("Test.Bool", false).Should().BeTrue();
    }

    [Fact]
    public void HasKeyAndRemove_ShouldWorkExpectedly()
    {
        _service.Set("Test.TempKey", "Value");
        _service.HasKey("Test.TempKey").Should().BeTrue();

        _service.Remove("Test.TempKey");
        _service.HasKey("Test.TempKey").Should().BeFalse();
    }

    public record TestConfigRecord(string Name, int Scale, bool Enabled);

    [Fact]
    public void SetAndGet_ComplexRecord_ShouldSerializeAndDeserialize()
    {
        var original = new TestConfigRecord("LayoutPreset", 100, true);
        _service.Set("Test.Record", original);

        var retrieved = _service.Get<TestConfigRecord?>("Test.Record", null);

        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("LayoutPreset");
        retrieved.Scale.Should().Be(100);
        retrieved.Enabled.Should().BeTrue();
    }
}
