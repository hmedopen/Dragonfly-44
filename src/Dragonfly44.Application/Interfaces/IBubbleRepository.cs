using Dragonfly44.Domain.Entities;

namespace Dragonfly44.Application.Interfaces;

public interface IBubbleRepository
{
    Task<Bubble?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Bubble>> GetByPageIdAsync(Guid pageId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Bubble>> GetByChapterIdAsync(Guid chapterId, CancellationToken cancellationToken = default);
    Task AddAsync(Bubble bubble, CancellationToken cancellationToken = default);
    Task UpdateAsync(Bubble bubble, CancellationToken cancellationToken = default);
    Task UpdateOrderAsync(IReadOnlyList<Bubble> bubbles, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
