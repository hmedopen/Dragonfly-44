using Dragonfly44.Application.Interfaces;
using Dragonfly44.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dragonfly44.Infrastructure.Persistence.Repositories;

public class BubbleRepository : IBubbleRepository
{
    private readonly Dragonfly44DbContext _dbContext;

    public BubbleRepository(Dragonfly44DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Bubble?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Bubbles
            .Include(b => b.Page)
            .Include(b => b.Character)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Bubble>> GetByPageIdAsync(Guid pageId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Bubbles
            .Include(b => b.Page)
            .Where(b => b.PageId == pageId)
            .OrderBy(b => b.ReadingOrderIndex)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Bubble>> GetByChapterIdAsync(Guid chapterId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Bubbles
            .Include(b => b.Page)
            .Where(b => b.Page != null && b.Page.ChapterId == chapterId)
            .OrderBy(b => b.Page!.PageNumber)
            .ThenBy(b => b.ReadingOrderIndex)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Bubble bubble, CancellationToken cancellationToken = default)
    {
        await _dbContext.Bubbles.AddAsync(bubble, cancellationToken);
    }

    public Task UpdateAsync(Bubble bubble, CancellationToken cancellationToken = default)
    {
        _dbContext.Bubbles.Update(bubble);
        return Task.CompletedTask;
    }

    public Task UpdateOrderAsync(IReadOnlyList<Bubble> bubbles, CancellationToken cancellationToken = default)
    {
        _dbContext.Bubbles.UpdateRange(bubbles);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var bubble = await _dbContext.Bubbles.FindAsync(new object?[] { id }, cancellationToken);
        if (bubble != null)
        {
            _dbContext.Bubbles.Remove(bubble);
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
