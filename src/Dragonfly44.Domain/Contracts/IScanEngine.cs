using Dragonfly44.Domain.Entities;

namespace Dragonfly44.Domain.Contracts;

public interface IScanEngine
{
    string ProviderId { get; }
    Task<ScanResult> ScanPageAsync(Page page, CancellationToken cancellationToken = default);
    Task<AuditResult> AuditChapterAsync(Chapter chapter, CancellationToken cancellationToken = default);
}
