using Dragonfly44.Application.AI.Models;
using Dragonfly44.Domain.Entities;

namespace Dragonfly44.Application.Services;

/// <summary>
/// Central orchestration service for AI translation, model configuration, connectivity testing,
/// and translation history lifecycle with manual translation protection.
/// </summary>
public interface ITranslationService
{
    Task<TranslationResult> TranslateDialogueAsync(
        Guid bubbleId,
        string? providerName = null,
        string? modelId = null,
        double? temperature = null,
        int? maxTokens = null,
        CancellationToken cancellationToken = default);

    Task<bool> AcceptTranslationAsync(Guid historyEntryId, CancellationToken cancellationToken = default);
    Task<bool> RejectTranslationAsync(Guid historyEntryId, CancellationToken cancellationToken = default);
    Task<bool> RevertToManualTranslationAsync(Guid bubbleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TranslationHistoryEntry>> GetTranslationHistoryAsync(Guid bubbleId, CancellationToken cancellationToken = default);

    Task<ConnectionTestResult> TestProviderConnectionAsync(string providerName, string? modelId = null, string? customEndpoint = null, CancellationToken cancellationToken = default);
}
