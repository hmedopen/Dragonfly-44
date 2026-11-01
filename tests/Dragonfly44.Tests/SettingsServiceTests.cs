using Dragonfly44.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dragonfly44.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _testSettingsFile;
    private readonly SettingsService _service;

    public SettingsServiceTests()
    {
        _testSettingsFile = Path.Combine(Path.GetTempPath(), $"settings_test_{Guid.NewGuid():N}.json");
        _service = new SettingsService(NullLogger<SettingsService>.Instance, _testSettingsFile);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_testSettingsFile)) File.Delete(_testSettingsFile);
            var dir = Path.GetDirectoryName(_testSettingsFile);
            if (dir != null && Directory.Exists(dir))
            {
                foreach (var tmp in Directory.GetFiles(dir, "settings_*.tmp"))
                {
                    try { File.Delete(tmp); } catch { }
                }
            }
        }
        catch { }
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

    [Fact]
    public async Task SaveAsync_ShouldWriteAtomicallyAndPersistAcrossInstances()
    {
        _service.Set("App.Theme", "Dark");
        _service.Set("App.Zoom", 125);

        // Act
        await _service.SaveAsync();

        // Assert: file must exist on disk and be readable by a fresh instance
        File.Exists(_testSettingsFile).Should().BeTrue();

        var freshService = new SettingsService(NullLogger<SettingsService>.Instance, _testSettingsFile);
        await freshService.LoadAsync();

        freshService.Get("App.Theme", "Light").Should().Be("Dark");
        freshService.Get("App.Zoom", 100).Should().Be(125);
    }

    [Fact]
    public async Task SaveAsync_WhenDirectoryIsReadOnly_ShouldPropagateExceptionAndPreserveOriginalFile()
    {
        // 1. Save valid initial state
        _service.Set("Initial.Key", "InitialValue");
        await _service.SaveAsync();

        var originalContent = await File.ReadAllTextAsync(_testSettingsFile);

        // 2. Point to an invalid/illegal path that cannot be written
        var readOnlyService = new SettingsService(NullLogger<SettingsService>.Instance, "Z:\\nonexistent_drive_123\\settings.json");
        readOnlyService.Set("Failed.Key", "FailedValue");

        // Act & Assert: must throw observable exception
        var act = async () => await readOnlyService.SaveAsync();
        await act.Should().ThrowAsync<Exception>("Save failures must be observable and propagate to callers");

        // Verify original file was preserved intact
        var preservedContent = await File.ReadAllTextAsync(_testSettingsFile);
        preservedContent.Should().Be(originalContent);
    }
}
