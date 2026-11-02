using Dragonfly44.Application.Common;
using Dragonfly44.Domain.Entities;

namespace Dragonfly44.Application.Services;

public interface IDraftService
{
    Task<Result<Draft>> CreateDraftAsync(string name, string? description = null, CancellationToken cancellationToken = default);
    Task<Result<Draft>> RenameDraftAsync(Guid draftId, string newName, CancellationToken cancellationToken = default);
    Task<Result> DeleteDraftAsync(Guid draftId, CancellationToken cancellationToken = default);
    Task<Draft?> GetDraftAsync(Guid draftId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Draft>> GetAllDraftsAsync(CancellationToken cancellationToken = default);
}
