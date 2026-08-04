using KorrnellHelper.Application.Documents;
using KorrnellHelper.Domain.Documents;

namespace KorrnellHelper.Tests.TestSupport;

/// <summary>Shared by every test exercising a handler that scans via GetAllAsync (not vector search).</summary>
public sealed class FakeDocumentChunkStore(IReadOnlyList<DocumentChunk> chunks) : IDocumentChunkStore
{
    public Task AddRangeAsync(IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<DocumentChunk>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(chunks);
}
