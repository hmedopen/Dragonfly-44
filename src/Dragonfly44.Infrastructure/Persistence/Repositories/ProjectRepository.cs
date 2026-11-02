using Dragonfly44.Application.Interfaces;
using Dragonfly44.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dragonfly44.Infrastructure.Persistence.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly Dragonfly44DbContext _dbContext;

    public ProjectRepository(Dragonfly44DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Projects
            .Include(p => p.Chapters)
                .ThenInclude(c => c.Pages)
                    .ThenInclude(pg => pg.Bubbles)
            .Include(p => p.Characters)
            .Include(p => p.GlossaryEntries)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Projects
            .Include(p => p.Chapters)
                .ThenInclude(c => c.Pages)
            .OrderByDescending(p => p.UpdatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Project project, CancellationToken cancellationToken = default)
    {
        await _dbContext.Projects.AddAsync(project, cancellationToken);
    }

    public Task UpdateAsync(Project project, CancellationToken cancellationToken = default)
    {
        project.UpdatedAtUtc = DateTime.UtcNow;
        _dbContext.Projects.Update(project);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var project = await _dbContext.Projects.FindAsync(new object?[] { id }, cancellationToken);
        if (project != null)
        {
            _dbContext.Projects.Remove(project);
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
