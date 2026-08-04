using KorrnellHelper.Application.Ai;
using KorrnellHelper.Application.Reminders;
using KorrnellHelper.Domain.Documents;
using KorrnellHelper.Tests.TestSupport;
using Xunit;

namespace KorrnellHelper.Tests.Reminders;

public class GenerateReminderDigestQueryHandlerTests
{
    private sealed class FakeAnswerGenerator : IAnswerGenerator
    {
        public string? LastPrompt { get; private set; }
        public string ResponseToReturn { get; set; } = "1. 測試活動(8/13～8/17)\n• 辦理事項：測試";
        public int CallCount { get; private set; }

        public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
        {
            LastPrompt = prompt;
            CallCount++;
            return Task.FromResult(ResponseToReturn);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // 10:00 UTC == 18:00 Taipei — safely the same calendar day (2026-08-05) under either clock,
    // so this instant can't accidentally hide a UTC-vs-local-date bug.
    private static readonly DateTimeOffset ReferenceInstant = new(2026, 8, 5, 10, 0, 0, TimeSpan.Zero);

    private static DocumentChunk MakeChunk(
        string heading, string content, int? schoolYear = null, DateOnly? publishedDate = null)
    {
        return new DocumentChunk
        {
            Id = Guid.NewGuid(),
            SourceDocument = "doc.md",
            Heading = heading,
            Content = content,
            Embedding = [1f],
            SchoolYear = schoolYear,
            PublishedDate = publishedDate,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    private static GenerateReminderDigestQueryHandler CreateHandler(
        IReadOnlyList<DocumentChunk> chunks, FakeAnswerGenerator answerGenerator) =>
        new(new FakeDocumentChunkStore(chunks), answerGenerator, new FixedTimeProvider(ReferenceInstant));

    [Fact]
    public async Task HandleAsync_DeduplicatesChunksByTopicBeforeBuildingThePrompt()
    {
        var oldYear = MakeChunk("才藝選課", "114學年度：舊資訊", schoolYear: 114);
        var newYear = MakeChunk("才藝選課", "115學年度：新資訊", schoolYear: 115);
        var answerGenerator = new FakeAnswerGenerator();
        var handler = CreateHandler([oldYear, newYear], answerGenerator);

        await handler.HandleAsync();

        Assert.Contains("115學年度：新資訊", answerGenerator.LastPrompt);
        Assert.DoesNotContain("114學年度：舊資訊", answerGenerator.LastPrompt);
    }

    [Fact]
    public async Task HandleAsync_IncludesTodaysTaipeiDateInThePrompt()
    {
        var answerGenerator = new FakeAnswerGenerator();
        var handler = CreateHandler([MakeChunk("活動", "內容")], answerGenerator);

        await handler.HandleAsync();

        Assert.Contains("2026-08-05", answerGenerator.LastPrompt);
    }

    [Fact]
    public async Task HandleAsync_PromptRequestsDateRangeOverlapNotJustAPointInWindow()
    {
        var answerGenerator = new FakeAnswerGenerator();
        var handler = CreateHandler([MakeChunk("活動", "內容")], answerGenerator);

        await handler.HandleAsync();

        Assert.Contains("重疊", answerGenerator.LastPrompt);
    }

    [Fact]
    public async Task HandleAsync_AiReturnsActivities_ReturnsFormattedDigestWithDisclaimerFooter()
    {
        var answerGenerator = new FakeAnswerGenerator
        {
            ResponseToReturn = "1. 課後才藝選課(8/13～8/17)\n• 辦理事項：線上選課",
        };
        var handler = CreateHandler([MakeChunk("才藝選課", "內容")], answerGenerator);

        var result = await handler.HandleAsync();

        Assert.True(result.HasActivities);
        Assert.Contains("課後才藝選課", result.DigestText);
        Assert.Contains("Korrnell APP", result.DigestText);
        Assert.Contains("系統自動整理", result.DigestText);
    }

    [Fact]
    public async Task HandleAsync_AiGeneratedTextContainsMarkdown_StrippedForLine()
    {
        var answerGenerator = new FakeAnswerGenerator { ResponseToReturn = "1. **重要活動**(8/13～8/17)" };
        var handler = CreateHandler([MakeChunk("活動", "內容")], answerGenerator);

        var result = await handler.HandleAsync();

        Assert.DoesNotContain("**", result.DigestText);
        Assert.Contains("重要活動", result.DigestText);
    }

    [Fact]
    public async Task HandleAsync_AiRespondsWithNoActivitiesSentinel_ReturnsHasActivitiesFalse()
    {
        var answerGenerator = new FakeAnswerGenerator { ResponseToReturn = "NO_UPCOMING_ACTIVITIES" };
        var handler = CreateHandler([MakeChunk("活動", "內容")], answerGenerator);

        var result = await handler.HandleAsync();

        Assert.False(result.HasActivities);
        Assert.False(string.IsNullOrWhiteSpace(result.DigestText));
    }

    [Fact]
    public async Task HandleAsync_NoChunksInStore_ReturnsHasActivitiesFalseWithoutCallingAi()
    {
        var answerGenerator = new FakeAnswerGenerator();
        var handler = CreateHandler([], answerGenerator);

        var result = await handler.HandleAsync();

        Assert.False(result.HasActivities);
        Assert.Equal(0, answerGenerator.CallCount);
    }
}
