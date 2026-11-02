using Dragonfly44.Application.Common;
using Dragonfly44.Application.Interfaces;
using Dragonfly44.Application.Services;
using Dragonfly44.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Dragonfly44.Infrastructure.Services;

public class DraftService : IDraftService
{
    private readonly IDraftRepository _draftRepository;
    private readonly IStorageService _storageService;
    private readonly ILogger<DraftService> _logger;

    public DraftService(
        IDraftRepository draftRepository,
        IStorageService storageService,
        ILogger<DraftService> logger)
    {
        _draftRepository = draftRepository;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<Result<Draft>> CreateDraftAsync(
        string name,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<Draft>.Failure("Draft name cannot be empty.");
        }

        var draft = new Draft
        {
            Name = name.Trim(),
            Description = description?.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        try
        {
            await _draftRepository.AddAsync(draft, cancellationToken);
            await _draftRepository.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Created draft {DraftId} '{DraftName}'", draft.Id, draft.Name);
            return Result<Draft>.Success(draft);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create draft '{DraftName}'", name);
            return Result<Draft>.Failure($"Failed to create draft: {ex.Message}");
        }
    }

    public async Task<Result<Draft>> RenameDraftAsync(
        Guid draftId,
        string newName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            return Result<Draft>.Failure("Draft name cannot be empty.");
        }

        try
        {
            var draft = await _draftRepository.GetByIdAsync(draftId, cancellationToken);
            if (draft == null)
            {
                return Result<Draft>.Failure($"Draft with ID {draftId} was not found.");
            }

            draft.Name = newName.Trim();
            draft.UpdatedAtUtc = DateTime.UtcNow;

            await _draftRepository.UpdateAsync(draft, cancellationToken);
            await _draftRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Renamed draft {DraftId} to '{DraftName}'", draftId, draft.Name);
            return Result<Draft>.Success(draft);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rename draft {DraftId}", draftId);
            return Result<Draft>.Failure($"Failed to rename draft: {ex.Message}");
        }
    }

    public async Task<Result> DeleteDraftAsync(Guid draftId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _draftRepository.DeleteAsync(draftId, cancellationToken);
            await _draftRepository.SaveChangesAsync(cancellationToken);

            // Clean up managed storage for draft
            await _storageService.DeleteDraftStorageAsync(draftId, cancellationToken);

            _logger.LogInformation("Deleted draft {DraftId} and associated storage", draftId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete draft {DraftId}", draftId);
            return Result.Failure(ex.Message);
        }
    }

    public Task<Draft?> GetDraftAsync(Guid draftId, CancellationToken cancellationToken = default) =>
        _draftRepository.GetByIdAsync(draftId, cancellationToken);

    public Task<IReadOnlyList<Draft>> GetAllDraftsAsync(CancellationToken cancellationToken = default) =>
        _draftRepository.GetAllAsync(cancellationToken);
}
