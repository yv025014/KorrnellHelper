using KorrnellHelper.Application.Ai;
using KorrnellHelper.Application.Documents;
using KorrnellHelper.Domain.Documents;
using Xunit;

namespace KorrnellHelper.Tests.Documents;

public class AddDocumentCommandHandlerTests
{
    private sealed class FakeEmbeddingGenerator : IEmbeddingGenerator
    {
        public List<string> EmbeddedTexts { get; } = [];

        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
        {
            EmbeddedTexts.Add(text);
            // Distinguishable per call so tests can tell embeddings apart.
            return Task.FromResult(new float[] { EmbeddedTexts.Count });
        }
    }

    private sealed class FakeDocumentChunkStore : IDocumentChunkStore
    {
        public IReadOnlyList<DocumentChunk>? SavedChunks { get; private set; }

        public Task AddRangeAsync(IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default)
        {
            SavedChunks = chunks;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DocumentChunk>> GetAllAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not exercised by AddDocumentCommandHandler.");
    }

    private const string TwoSectionMarkdown = """
        # 小一暑期銜接課程暨新生訓練須知

        開學前準備事項。

        ## 服裝儀容

        請穿著便服。
        """;

    [Fact]
    public async Task HandleAsync_EmbedsEachSectionSeparately()
    {
        var embeddingGenerator = new FakeEmbeddingGenerator();
        var store = new FakeDocumentChunkStore();
        var handler = new AddDocumentCommandHandler(embeddingGenerator, store, TimeProvider.System);
        var command = new AddDocumentCommand("notice.md", TwoSectionMarkdown, SchoolYear: 115, PublishedDate: new DateOnly(2026, 7, 31));

        await handler.HandleAsync(command);

        Assert.Equal(2, embeddingGenerator.EmbeddedTexts.Count);
        Assert.Contains("開學前準備事項", embeddingGenerator.EmbeddedTexts[0]);
        Assert.Contains("請穿著便服", embeddingGenerator.EmbeddedTexts[1]);
    }

    [Fact]
    public async Task HandleAsync_StoresOneChunkPerSection_WithCommandMetadataAndItsOwnEmbedding()
    {
        var embeddingGenerator = new FakeEmbeddingGenerator();
        var store = new FakeDocumentChunkStore();
        var handler = new AddDocumentCommandHandler(embeddingGenerator, store, TimeProvider.System);
        var command = new AddDocumentCommand("notice.md", TwoSectionMarkdown, SchoolYear: 115, PublishedDate: new DateOnly(2026, 7, 31));

        await handler.HandleAsync(command);

        Assert.NotNull(store.SavedChunks);
        Assert.Equal(2, store.SavedChunks!.Count);

        var first = store.SavedChunks[0];
        Assert.Equal("notice.md", first.SourceDocument);
        Assert.Equal("小一暑期銜接課程暨新生訓練須知", first.Heading);
        Assert.Equal(115, first.SchoolYear);
        Assert.Equal(new DateOnly(2026, 7, 31), first.PublishedDate);
        Assert.Equal([1f], first.Embedding);

        var second = store.SavedChunks[1];
        Assert.Equal("服裝儀容", second.Heading);
        Assert.Equal([2f], second.Embedding);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task HandleAsync_ReturnsChunkCount()
    {
        var handler = new AddDocumentCommandHandler(
            new FakeEmbeddingGenerator(), new FakeDocumentChunkStore(), TimeProvider.System);
        var command = new AddDocumentCommand("notice.md", TwoSectionMarkdown, SchoolYear: null, PublishedDate: null);

        var count = await handler.HandleAsync(command);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task HandleAsync_BlankMarkdown_StoresNothingAndReturnsZero()
    {
        var embeddingGenerator = new FakeEmbeddingGenerator();
        var store = new FakeDocumentChunkStore();
        var handler = new AddDocumentCommandHandler(embeddingGenerator, store, TimeProvider.System);
        var command = new AddDocumentCommand("notice.md", "   ", SchoolYear: null, PublishedDate: null);

        var count = await handler.HandleAsync(command);

        Assert.Equal(0, count);
        Assert.Empty(embeddingGenerator.EmbeddedTexts);
    }
}
