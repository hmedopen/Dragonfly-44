using Dragonfly44.Application.Services;
using Dragonfly44.Domain.Contracts;
using Dragonfly44.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dragonfly44.Tests;

public class ScanEngineContractTests
{
    private class StubScanEngine : IScanEngine
    {
        public string ProviderId => "stub-engine-test";

        public Task<ScanResult> ScanPageAsync(Page page, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bubbles = new List<DetectedBubbleDto>
            {
                new(10, 20, 100, 50, "こんにちは", 0.95)
            };
            return Task.FromResult(ScanResult.Succeeded(bubbles));
        }

        public Task<AuditResult> AuditChapterAsync(Chapter chapter, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var findings = new List<AuditFinding>
            {
                new("ORD001", "Reading order check passed", "Info", PageNumber: 1)
            };
            return Task.FromResult(AuditResult.Succeeded(findings));
        }
    }

    [Fact]
    public async Task ScanEngineRegistry_ShouldRegisterAndRetrieveEngine()
    {
        // Arrange
        var registry = new ScanEngineRegistry(NullLogger<ScanEngineRegistry>.Instance);
        var stub = new StubScanEngine();

        // Act
        registry.Register(stub);
        var resolved = registry.GetProvider("stub-engine-test");
        var available = registry.GetAvailableProviders();

        // Assert
        resolved.Should().NotBeNull();
        resolved!.ProviderId.Should().Be("stub-engine-test");
        available.Should().Contain("stub-engine-test");

        // Test ScanPageAsync contract
        var page = new Page { PageNumber = 1 };
        var scanResult = await resolved.ScanPageAsync(page);
        scanResult.Success.Should().BeTrue();
        scanResult.DetectedBubbles.Should().HaveCount(1);
        scanResult.DetectedBubbles[0].RawText.Should().Be("こんにちは");

        // Test AuditChapterAsync contract
        var chapter = new Chapter { ChapterNumber = 1 };
        var auditResult = await resolved.AuditChapterAsync(chapter);
        auditResult.Success.Should().BeTrue();
        auditResult.Findings.Should().HaveCount(1);
    }

    [Fact]
    public async Task ScanEngine_ShouldHonorCancellationToken()
    {
        // Arrange
        var stub = new StubScanEngine();
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel

        // Act & Assert
        var actScan = () => stub.ScanPageAsync(new Page(), cts.Token);
        await actScan.Should().ThrowAsync<OperationCanceledException>();

        var actAudit = () => stub.AuditChapterAsync(new Chapter(), cts.Token);
        await actAudit.Should().ThrowAsync<OperationCanceledException>();
    }
}
