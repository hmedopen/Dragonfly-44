using Dragonfly44.Application.Interfaces;
using Dragonfly44.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dragonfly44.Infrastructure.Persistence.Repositories;

public class PageRepository : IPageRepository
{
    private readonly Dragonfly44DbContext _dbContext;

    public PageRepository(Dragonfly44DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Page?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Pages
            .Include(p => p.Bubbles)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Page>> GetByChapterIdAsync(Guid chapterId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Pages
            .Include(p => p.Bubbles)
            .Where(p => p.ChapterId == chapterId)
            .OrderBy(p => p.PageNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<Page> pages, CancellationToken cancellationToken = default)
    {
        await _dbContext.Pages.AddRangeAsync(pages, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var page = await _dbContext.Pages.FindAsync(new object?[] { id }, cancellationToken);
        if (page != null)
        {
            _dbContext.Pages.Remove(page);
        }
    }

    public async Task DeleteByChapterIdAsync(Guid chapterId, CancellationToken cancellationToken = default)
    {
        var pages = await _dbContext.Pages
            .Where(p => p.ChapterId == chapterId)
            .ToListAsync(cancellationToken);

        if (pages.Count > 0)
        {
            _dbContext.Pages.RemoveRange(pages);
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
