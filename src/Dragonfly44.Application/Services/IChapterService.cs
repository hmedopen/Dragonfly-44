using Dragonfly44.Application.Common;
using Dragonfly44.Domain.Entities;

namespace Dragonfly44.Application.Services;

public interface IChapterService
{
    Task<Result<Chapter>> CreateChapterAsync(Guid projectId, string title, int? chapterNumber = null, CancellationToken cancellationToken = default);
    Task<Result<Chapter>> RenameChapterAsync(Guid chapterId, string newTitle, CancellationToken cancellationToken = default);
    Task<Result> DeleteChapterAsync(Guid chapterId, CancellationToken cancellationToken = default);
    Task<Chapter?> GetChapterAsync(Guid chapterId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Chapter>> GetChaptersByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}
