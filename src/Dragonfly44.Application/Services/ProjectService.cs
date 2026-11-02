using Dragonfly44.Application.Common;
using Dragonfly44.Application.Interfaces;
using Dragonfly44.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Dragonfly44.Application.Services;

public class ProjectService : IProjectService
{
    private readonly IProjectRepository _projectRepository;
    private readonly IStorageService? _storageService;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(
        IProjectRepository projectRepository,
        ILogger<ProjectService> logger,
        IStorageService? storageService = null)
    {
        _projectRepository = projectRepository;
        _logger = logger;
        _storageService = storageService;
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

    public async Task<Result<Project>> RenameProjectAsync(Guid id, string newName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return Result<Project>.Failure("Project name cannot be empty.");
        }

        try
        {
            var project = await _projectRepository.GetByIdAsync(id, cancellationToken);
            if (project == null)
            {
                return Result<Project>.Failure($"Project with ID {id} was not found.");
            }

            project.Name = newName.Trim();
            project.UpdatedAtUtc = DateTime.UtcNow;

            await _projectRepository.UpdateAsync(project, cancellationToken);
            await _projectRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Renamed project {ProjectId} to '{ProjectName}'", id, project.Name);
            return Result<Project>.Success(project);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rename project {ProjectId}", id);
            return Result<Project>.Failure($"Failed to rename project: {ex.Message}");
        }
    }

    public async Task<Result> DeleteProjectAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _projectRepository.DeleteAsync(id, cancellationToken);
            await _projectRepository.SaveChangesAsync(cancellationToken);

            if (_storageService != null)
            {
                await _storageService.DeleteProjectStorageAsync(id, cancellationToken);
            }

            _logger.LogInformation("Deleted project {ProjectId} and associated storage", id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete project {ProjectId}", id);
            return Result.Failure(ex.Message);
        }
    }
}
