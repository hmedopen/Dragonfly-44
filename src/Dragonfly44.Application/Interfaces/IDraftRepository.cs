using Dragonfly44.Domain.Entities;

namespace Dragonfly44.Application.Interfaces;

public interface IDraftRepository
{
    Task<Draft?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Draft>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Draft draft, CancellationToken cancellationToken = default);
    Task UpdateAsync(Draft draft, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
