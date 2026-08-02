using KorrnellHelper.Application.Ai;
using KorrnellHelper.Application.Documents;
using KorrnellHelper.Domain.Documents;
using Xunit;

namespace KorrnellHelper.Tests.Documents;

public class AnswerQuestionQueryHandlerTests
{
    private sealed class FakeEmbeddingGenerator : IEmbeddingGenerator
    {
        public string? LastEmbeddedText { get; private set; }

        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
        {
            LastEmbeddedText = text;
            return Task.FromResult(new float[] { 1f });
        }
    }

    private sealed class FakeSearcher(IReadOnlyList<DocumentChunk> results) : IDocumentChunkSearcher
    {
        public float[]? LastQueryEmbedding { get; private set; }
        public int? LastLimit { get; private set; }

        public Task<IReadOnlyList<DocumentChunk>> SearchAsync(
            float[] queryEmbedding, int limit, CancellationToken cancellationToken = default)
        {
            LastQueryEmbedding = queryEmbedding;
            LastLimit = limit;
            return Task.FromResult(results);
        }
    }

    private sealed class FakeAnswerGenerator : IAnswerGenerator
    {
        public string? LastPrompt { get; private set; }
        public string ResponseToReturn { get; set; } = "fake answer";

        public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
        {
            LastPrompt = prompt;
            return Task.FromResult(ResponseToReturn);
        }
    }

    private static DocumentChunk MakeChunk(string heading, string content, int? schoolYear = null)
    {
        return new DocumentChunk
        {
            Id = Guid.NewGuid(),
            SourceDocument = "doc.md",
            Heading = heading,
            Content = content,
            Embedding = [1f],
            SchoolYear = schoolYear,
            PublishedDate = null,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    [Fact]
    public async Task HandleAsync_EmbedsTheQuestion()
    {
        var embeddingGenerator = new FakeEmbeddingGenerator();
        var handler = new AnswerQuestionQueryHandler(
            embeddingGenerator, new FakeSearcher([MakeChunk("服裝儀容", "請穿便服")]), new FakeAnswerGenerator());

        await handler.HandleAsync(new AnswerQuestionQuery("開學要穿什麼?"));

        Assert.Equal("開學要穿什麼?", embeddingGenerator.LastEmbeddedText);
    }

    [Fact]
    public async Task HandleAsync_SearchesWithTheQuestionEmbedding()
    {
        var searcher = new FakeSearcher([MakeChunk("服裝儀容", "請穿便服")]);
        var handler = new AnswerQuestionQueryHandler(new FakeEmbeddingGenerator(), searcher, new FakeAnswerGenerator());

        await handler.HandleAsync(new AnswerQuestionQuery("開學要穿什麼?"));

        Assert.Equal([1f], searcher.LastQueryEmbedding);
        Assert.True(searcher.LastLimit > 0);
    }

    [Fact]
    public async Task HandleAsync_DeduplicatesSearchResultsByTopicBeforeBuildingThePrompt()
    {
        var oldYear = MakeChunk("服裝儀容", "114學年度：請穿便服", schoolYear: 114);
        var newYear = MakeChunk("服裝儀容", "115學年度：請穿制服", schoolYear: 115);
        var answerGenerator = new FakeAnswerGenerator();
        var handler = new AnswerQuestionQueryHandler(
            new FakeEmbeddingGenerator(), new FakeSearcher([oldYear, newYear]), answerGenerator);

        await handler.HandleAsync(new AnswerQuestionQuery("開學要穿什麼?"));

        Assert.Contains("115學年度：請穿制服", answerGenerator.LastPrompt);
        Assert.DoesNotContain("114學年度：請穿便服", answerGenerator.LastPrompt);
    }

    [Fact]
    public async Task HandleAsync_IncludesTheQuestionInThePrompt()
    {
        var answerGenerator = new FakeAnswerGenerator();
        var handler = new AnswerQuestionQueryHandler(
            new FakeEmbeddingGenerator(), new FakeSearcher([MakeChunk("服裝儀容", "請穿便服")]), answerGenerator);

        await handler.HandleAsync(new AnswerQuestionQuery("開學要穿什麼?"));

        Assert.Contains("開學要穿什麼?", answerGenerator.LastPrompt);
    }

    [Fact]
    public async Task HandleAsync_ReturnsGeneratedAnswerAndUsedChunks()
    {
        var chunk = MakeChunk("服裝儀容", "請穿便服");
        var answerGenerator = new FakeAnswerGenerator { ResponseToReturn = "開學要穿便服喔" };
        var handler = new AnswerQuestionQueryHandler(
            new FakeEmbeddingGenerator(), new FakeSearcher([chunk]), answerGenerator);

        var result = await handler.HandleAsync(new AnswerQuestionQuery("開學要穿什麼?"));

        Assert.Equal("開學要穿便服喔", result.Answer);
        Assert.Single(result.UsedChunks);
        Assert.Equal(chunk.Id, result.UsedChunks[0].Id);
    }

    [Fact]
    public async Task HandleAsync_NoSearchResults_ReturnsFallbackWithoutCallingGenerate()
    {
        var answerGenerator = new FakeAnswerGenerator();
        var handler = new AnswerQuestionQueryHandler(
            new FakeEmbeddingGenerator(), new FakeSearcher([]), answerGenerator);

        var result = await handler.HandleAsync(new AnswerQuestionQuery("開學要穿什麼?"));

        Assert.Null(answerGenerator.LastPrompt);
        Assert.Empty(result.UsedChunks);
        Assert.False(string.IsNullOrWhiteSpace(result.Answer));
    }
}
