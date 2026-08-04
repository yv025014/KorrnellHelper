using KorrnellHelper.Domain.Documents;

namespace KorrnellHelper.Application.Documents;

public interface IDocumentChunkStore
{
    Task AddRangeAsync(IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every stored chunk, regardless of similarity to any query. Used by scans that must not
    /// silently miss anything (unlike <see cref="IDocumentChunkSearcher"/>'s top-k similarity
    /// search, which is tuned for "most relevant to one question," not completeness).
    /// </summary>
    Task<IReadOnlyList<DocumentChunk>> GetAllAsync(CancellationToken cancellationToken = default);
}
