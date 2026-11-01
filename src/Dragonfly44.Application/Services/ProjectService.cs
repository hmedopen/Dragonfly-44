using Dragonfly44.Application.Common;
using Dragonfly44.Application.Interfaces;
using Dragonfly44.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Dragonfly44.Application.Services;

public class ProjectService : IProjectService
{
    private readonly IProjectRepository _projectRepository;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(IProjectRepository projectRepository, ILogger<ProjectService> logger)
    {
        _projectRepository = projectRepository;
        _logger = logger;
    }

    public async Task<Result<Project>> CreateProjectAsync(
        string name,
        string sourceLang = "ja",
        string targetLang = "ar",
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<Project>.Failure("Project name cannot be empty.");
        }

        var project = new Project
        {
            Name = name.Trim(),
            SourceLanguage = sourceLang,
            TargetLanguage = targetLang,
            Description = description?.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        try
        {
            await _projectRepository.AddAsync(project, cancellationToken);
            await _projectRepository.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Successfully created project {ProjectId} '{ProjectName}'", project.Id, project.Name);
            return Result<Project>.Success(project);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create project '{ProjectName}'", name);
            return Result<Project>.Failure($"Failed to create project: {ex.Message}");
        }
    }

    public Task<Project?> GetProjectAsync(Guid id, CancellationToken cancellationToken = default) =>
        _projectRepository.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Project>> GetAllProjectsAsync(CancellationToken cancellationToken = default) =>
        _projectRepository.GetAllAsync(cancellationToken);

    public async Task<Result> DeleteProjectAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _projectRepository.DeleteAsync(id, cancellationToken);
            await _projectRepository.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Deleted project {ProjectId}", id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete project {ProjectId}", id);
            return Result.Failure(ex.Message);
        }
    }
}
