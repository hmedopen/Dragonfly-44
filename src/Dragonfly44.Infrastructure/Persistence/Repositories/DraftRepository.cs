using Dragonfly44.Application.Interfaces;
using Dragonfly44.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dragonfly44.Infrastructure.Persistence.Repositories;

public class DraftRepository : IDraftRepository
{
    private readonly Dragonfly44DbContext _dbContext;

    public DraftRepository(Dragonfly44DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Draft?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Drafts
            .Include(d => d.Pages.OrderBy(p => p.PageNumber))
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Draft>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Drafts
            .Include(d => d.Pages.OrderBy(p => p.PageNumber))
            .OrderByDescending(d => d.UpdatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Draft draft, CancellationToken cancellationToken = default)
    {
        await _dbContext.Drafts.AddAsync(draft, cancellationToken);
    }

    public Task UpdateAsync(Draft draft, CancellationToken cancellationToken = default)
    {
        draft.UpdatedAtUtc = DateTime.UtcNow;
        _dbContext.Drafts.Update(draft);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var draft = await _dbContext.Drafts.FindAsync(new object?[] { id }, cancellationToken);
        if (draft != null)
        {
            _dbContext.Drafts.Remove(draft);
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
