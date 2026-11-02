using Dragonfly44.Domain.Entities;

namespace Dragonfly44.Application.Interfaces;

public interface IPageRepository
{
    Task<Page?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Page>> GetByChapterIdAsync(Guid chapterId, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<Page> pages, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteByChapterIdAsync(Guid chapterId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
