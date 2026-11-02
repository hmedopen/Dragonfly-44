using Dragonfly44.Application.Interfaces;
using Dragonfly44.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dragonfly44.Infrastructure.Persistence.Repositories;

public class ChapterRepository : IChapterRepository
{
    private readonly Dragonfly44DbContext _dbContext;

    public ChapterRepository(Dragonfly44DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Chapter?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Chapters
            .Include(c => c.Pages.OrderBy(p => p.PageNumber))
            .Include(c => c.ChapterContext)
            .Include(c => c.Project)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Chapter>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Chapters
            .Include(c => c.Pages.OrderBy(p => p.PageNumber))
            .Where(c => c.ProjectId == projectId)
            .OrderBy(c => c.ChapterNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Chapter chapter, CancellationToken cancellationToken = default)
    {
        await _dbContext.Chapters.AddAsync(chapter, cancellationToken);
    }

    public Task UpdateAsync(Chapter chapter, CancellationToken cancellationToken = default)
    {
        chapter.UpdatedAtUtc = DateTime.UtcNow;
        _dbContext.Chapters.Update(chapter);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var chapter = await _dbContext.Chapters.FindAsync(new object?[] { id }, cancellationToken);
        if (chapter != null)
        {
            _dbContext.Chapters.Remove(chapter);
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
