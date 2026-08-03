using KorrnellHelper.Api.Line;
using KorrnellHelper.Application.Ai;
using KorrnellHelper.Application.Documents;
using KorrnellHelper.Domain.Documents;
using KorrnellHelper.Infrastructure.Line;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KorrnellHelper.Tests.Line;

public class LineWebhookHandlerTests
{
    private sealed class FakeEmbeddingGenerator : IEmbeddingGenerator
    {
        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default) =>
            Task.FromResult(new float[] { 1f });
    }

    private sealed class FakeSearcher(IReadOnlyList<DocumentChunk> results) : IDocumentChunkSearcher
    {
        public Task<IReadOnlyList<DocumentChunk>> SearchAsync(
            float[] queryEmbedding, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult(results);
    }

    private sealed class FakeAnswerGenerator : IAnswerGenerator
    {
        public string ResponseToReturn { get; set; } = "這是測試回答";
        public int CallCount { get; private set; }

        public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(ResponseToReturn);
        }
    }

    private sealed class FakeLineReplyClient : ILineReplyClient
    {
        public string? LastReplyToken { get; private set; }
        public string? LastText { get; private set; }
        public int CallCount { get; private set; }

        public Task ReplyAsync(string replyToken, string text, CancellationToken cancellationToken = default)
        {
            LastReplyToken = replyToken;
            LastText = text;
            CallCount++;
            return Task.CompletedTask;
        }
    }

    private const string AllowedUserId = "Uallowed123";
    private const string DisallowedUserId = "Ustranger456";

    private static (LineWebhookHandler Handler, FakeLineReplyClient ReplyClient, FakeAnswerGenerator AnswerGenerator)
        CreateHandler()
    {
        var chunk = new DocumentChunk
        {
            Id = Guid.NewGuid(),
            SourceDocument = "doc.md",
            Heading = "測試",
            Content = "測試內容",
            Embedding = [1f],
            SchoolYear = null,
            PublishedDate = null,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var answerGenerator = new FakeAnswerGenerator();
        var answerHandler = new AnswerQuestionQueryHandler(
            new FakeEmbeddingGenerator(), new FakeSearcher([chunk]), answerGenerator);
        var replyClient = new FakeLineReplyClient();
        var lineOptions = Options.Create(new LineOptions
        {
            ChannelSecret = "secret",
            ChannelAccessToken = "token",
            AllowedUserIds = AllowedUserId,
        });

        var handler = new LineWebhookHandler(
            answerHandler, replyClient, lineOptions, NullLogger<LineWebhookHandler>.Instance);

        return (handler, replyClient, answerGenerator);
    }

    [Fact]
    public async Task HandleEventAsync_AllowedUser_TextMessage_RepliesWithGeneratedAnswer()
    {
        var (handler, replyClient, answerGenerator) = CreateHandler();
        answerGenerator.ResponseToReturn = "開學是8/31喔";
        var lineEvent = new LineEvent
        {
            Type = "message",
            Message = new LineMessage { Type = "text", Text = "開學日是幾號?" },
            Source = new LineSource { Type = "user", UserId = AllowedUserId },
            ReplyToken = "reply-token-abc",
        };

        await handler.HandleEventAsync(lineEvent);

        Assert.Equal(1, replyClient.CallCount);
        Assert.Equal("reply-token-abc", replyClient.LastReplyToken);
        Assert.Equal("開學是8/31喔", replyClient.LastText);
    }

    [Fact]
    public async Task HandleEventAsync_DisallowedUser_RepliesWithTheirOwnUserIdButNeverCallsTheAnswerGenerator()
    {
        // Without this, a family member has no way to find their own LINE User ID to
        // give the admin — confirmed as a real onboarding gap during live testing.
        var (handler, replyClient, answerGenerator) = CreateHandler();
        var lineEvent = new LineEvent
        {
            Type = "message",
            Message = new LineMessage { Type = "text", Text = "開學日是幾號?" },
            Source = new LineSource { Type = "user", UserId = DisallowedUserId },
            ReplyToken = "reply-token-abc",
        };

        await handler.HandleEventAsync(lineEvent);

        Assert.Equal(1, replyClient.CallCount);
        Assert.Equal("reply-token-abc", replyClient.LastReplyToken);
        Assert.Contains(DisallowedUserId, replyClient.LastText);
        Assert.Equal(0, answerGenerator.CallCount);
    }

    [Fact]
    public async Task HandleEventAsync_NonMessageEvent_IsIgnored()
    {
        var (handler, replyClient, _) = CreateHandler();
        var lineEvent = new LineEvent
        {
            Type = "follow",
            Source = new LineSource { Type = "user", UserId = AllowedUserId },
            ReplyToken = "reply-token-abc",
        };

        await handler.HandleEventAsync(lineEvent);

        Assert.Equal(0, replyClient.CallCount);
    }

    [Fact]
    public async Task HandleEventAsync_NonTextMessage_IsIgnored()
    {
        var (handler, replyClient, _) = CreateHandler();
        var lineEvent = new LineEvent
        {
            Type = "message",
            Message = new LineMessage { Type = "sticker" },
            Source = new LineSource { Type = "user", UserId = AllowedUserId },
            ReplyToken = "reply-token-abc",
        };

        await handler.HandleEventAsync(lineEvent);

        Assert.Equal(0, replyClient.CallCount);
    }

    [Fact]
    public async Task HandleEventAsync_MissingUserId_NeverReplies()
    {
        var (handler, replyClient, _) = CreateHandler();
        var lineEvent = new LineEvent
        {
            Type = "message",
            Message = new LineMessage { Type = "text", Text = "hi" },
            Source = new LineSource { Type = "group", UserId = null },
            ReplyToken = "reply-token-abc",
        };

        await handler.HandleEventAsync(lineEvent);

        Assert.Equal(0, replyClient.CallCount);
    }
}
