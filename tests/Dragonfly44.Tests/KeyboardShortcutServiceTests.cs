using Dragonfly44.Application.Common.Models;
using Dragonfly44.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dragonfly44.Tests;

public class KeyboardShortcutServiceTests
{
    private readonly KeyboardShortcutService _service;

    public KeyboardShortcutServiceTests()
    {
        _service = new KeyboardShortcutService(NullLogger<KeyboardShortcutService>.Instance);
    }

    [Fact]
    public void Register_ShouldAddShortcutToRegisteredCollection()
    {
        var shortcut = new KeyboardShortcut("test.save", "S", KeyModifiers.Control, () => { });

        _service.Register(shortcut);

        _service.RegisteredShortcuts.Should().ContainSingle(s => s.Id == "test.save");
    }

    [Fact]
    public void Unregister_ShouldRemoveShortcut()
    {
        var shortcut = new KeyboardShortcut("test.undo", "Z", KeyModifiers.Control, () => { });
        _service.Register(shortcut);

        var removed = _service.Unregister("test.undo");

        removed.Should().BeTrue();
        _service.RegisteredShortcuts.Should().BeEmpty();
    }

    [Fact]
    public void HandleKeyDown_MatchingShortcut_ShouldExecuteActionAndReturnTrue()
    {
        var executed = false;
        var shortcut = new KeyboardShortcut("test.action", "A", KeyModifiers.Control, () => executed = true);
        _service.Register(shortcut);

        var handled = _service.HandleKeyDown("A", KeyModifiers.Control, isTextEditingContext: false);

        handled.Should().BeTrue();
        executed.Should().BeTrue();
    }

    [Fact]
    public void HandleKeyDown_MismatchedModifiers_ShouldNotExecuteAndReturnFalse()
    {
        var executed = false;
        var shortcut = new KeyboardShortcut("test.action", "A", KeyModifiers.Control, () => executed = true);
        _service.Register(shortcut);

        var handled = _service.HandleKeyDown("A", KeyModifiers.Alt, isTextEditingContext: false);

        handled.Should().BeFalse();
        executed.Should().BeFalse();
    }

    [Fact]
    public void HandleKeyDown_DuringTextEditing_WhenSuppressionEnabled_ShouldNotExecute()
    {
        var executed = false;
        // Global action that should NOT swallow typing inside a textbox (e.g. typing letter 'N')
        var shortcut = new KeyboardShortcut("test.editor.safe", "N", KeyModifiers.None, () => executed = true,
            AllowDuringTextEditing: false);
        _service.Register(shortcut);

        var handled = _service.HandleKeyDown("N", KeyModifiers.None, isTextEditingContext: true);

        handled.Should().BeFalse("shortcut must be suppressed when user is typing in text control");
        executed.Should().BeFalse();
    }

    [Fact]
    public void HandleKeyDown_DuringTextEditing_WhenAllowed_ShouldExecute()
    {
        var executed = false;
        // Function key or explicit global override (e.g. F11 or Ctrl+Q) allowed during text editing
        var shortcut = new KeyboardShortcut("test.fullscreen", "F11", KeyModifiers.None, () => executed = true,
            AllowDuringTextEditing: true);
        _service.Register(shortcut);

        var handled = _service.HandleKeyDown("F11", KeyModifiers.None, isTextEditingContext: true);

        handled.Should().BeTrue("shortcut marked AllowDuringTextEditing must execute even when text editor has focus");
        executed.Should().BeTrue();
    }

    [Fact]
    public void HandleKeyDown_HigherPriorityShortcut_ShouldExecuteFirst()
    {
        var executionOrder = new List<string>();
        var normalPriority = new KeyboardShortcut("low", "F1", KeyModifiers.None, () => executionOrder.Add("low"), Priority: 0);
        var highPriority = new KeyboardShortcut("high", "F1", KeyModifiers.None, () => executionOrder.Add("high"), Priority: 10);

        _service.Register(normalPriority);
        _service.Register(highPriority);

        var handled = _service.HandleKeyDown("F1", KeyModifiers.None, isTextEditingContext: false);

        handled.Should().BeTrue();
        executionOrder.Should().ContainSingle().Which.Should().Be("high");
    }
}
