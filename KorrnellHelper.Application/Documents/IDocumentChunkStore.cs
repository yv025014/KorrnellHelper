using KorrnellHelper.Domain.Documents;

namespace KorrnellHelper.Application.Documents;

public interface IDocumentChunkStore
{
    Task AddRangeAsync(IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default);
}
