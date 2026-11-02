using Dragonfly44.Domain.Entities;

namespace Dragonfly44.Application.Interfaces;

public interface IChapterRepository
{
    Task<Chapter?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Chapter>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task AddAsync(Chapter chapter, CancellationToken cancellationToken = default);
    Task UpdateAsync(Chapter chapter, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
