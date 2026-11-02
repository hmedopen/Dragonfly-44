using Dragonfly44.Application.Common;
using Dragonfly44.Domain.Entities;

namespace Dragonfly44.Application.Services;

public interface IProjectService
{
    Task<Result<Project>> CreateProjectAsync(string name, string sourceLang = "ja", string targetLang = "ar", string? description = null, CancellationToken cancellationToken = default);
    Task<Project?> GetProjectAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Project>> GetAllProjectsAsync(CancellationToken cancellationToken = default);
    Task<Result<Project>> RenameProjectAsync(Guid id, string newName, CancellationToken cancellationToken = default);
    Task<Result> DeleteProjectAsync(Guid id, CancellationToken cancellationToken = default);
}
