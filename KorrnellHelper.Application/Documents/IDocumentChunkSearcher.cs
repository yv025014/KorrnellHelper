using KorrnellHelper.Domain.Documents;

namespace KorrnellHelper.Application.Documents;

public interface IDocumentChunkSearcher
{
    /// <summary>Nearest chunks by vector similarity, closest match first.</summary>
    Task<IReadOnlyList<DocumentChunk>> SearchAsync(
        float[] queryEmbedding, int limit, CancellationToken cancellationToken = default);
}
