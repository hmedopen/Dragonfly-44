namespace Dragonfly44.Domain.Contracts;

public record AuditFinding(
    string Code,
    string Message,
    string Severity,
    int? PageNumber = null,
    Guid? BubbleId = null
);

public record AuditResult(
    bool Success,
    IReadOnlyList<AuditFinding> Findings,
    string? ErrorMessage = null
)
{
    public static AuditResult Succeeded(IReadOnlyList<AuditFinding> findings) =>
        new(true, findings);

    public static AuditResult Failed(string error) =>
        new(false, Array.Empty<AuditFinding>(), error);
}
