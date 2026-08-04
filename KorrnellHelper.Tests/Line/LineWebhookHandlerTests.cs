using KorrnellHelper.Api.Line;
using KorrnellHelper.Application.Ai;
using KorrnellHelper.Application.Documents;
using KorrnellHelper.Application.Line;
using KorrnellHelper.Domain.Documents;
using KorrnellHelper.Infrastructure.Line;
using KorrnellHelper.Tests.TestSupport;
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
        public string? LastPrompt { get; private set; }

        public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastPrompt = prompt;
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

    // All four are "U" + exactly 32 lowercase hex chars, matching LineUserIdFormat's
    // real requirement — NewUserToAddId in particular must pass validation for the
    // "successfully added" test cases to actually exercise that path.
    private const string AdminUserId = "Uabcdef0123456789abcdef0123456789";
    private const string DisallowedUserId = "U0123456789abcdef0123456789abcdef";
    private const string DbAllowedUserId = "Ufedcba9876543210fedcba9876543210";
    private const string NewUserToAddId = "U1a2b3c4d5e6f78901a2b3c4d5e6f7890";

    private static (
        LineWebhookHandler Handler,
        FakeLineReplyClient ReplyClient,
        FakeAnswerGenerator AnswerGenerator,
        FakeAllowedUserStore AllowedUserStore)
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
        var allowedUserStore = new FakeAllowedUserStore();
        allowedUserStore.Users.Add(DbAllowedUserId);
        var addAllowedUserHandler = new AddAllowedUserCommandHandler(allowedUserStore);
        var lineOptions = Options.Create(new LineOptions
        {
            ChannelSecret = "secret",
            ChannelAccessToken = "token",
            AllowedUserIds = AdminUserId,
        });

        var handler = new LineWebhookHandler(
            answerHandler,
            addAllowedUserHandler,
            allowedUserStore,
            replyClient,
            lineOptions,
            NullLogger<LineWebhookHandler>.Instance);

        return (handler, replyClient, answerGenerator, allowedUserStore);
    }

    private static LineEvent TextMessageFrom(string userId, string text) => new()
    {
        Type = "message",
        Message = new LineMessage { Type = "text", Text = text },
        Source = new LineSource { Type = "user", UserId = userId },
        ReplyToken = "reply-token-abc",
    };

    [Fact]
    public async Task HandleEventAsync_AdminUser_TextMessage_RepliesWithGeneratedAnswer()
    {
        var (handler, replyClient, answerGenerator, _) = CreateHandler();
        answerGenerator.ResponseToReturn = "開學是8/31喔";

        await handler.HandleEventAsync(TextMessageFrom(AdminUserId, "開學日是幾號?"));

        Assert.Equal(1, replyClient.CallCount);
        Assert.Equal("開學是8/31喔", replyClient.LastText);
    }

    [Fact]
    public async Task HandleEventAsync_GeneratedAnswerContainsMarkdown_RepliesWithLineFormattedText()
    {
        var (handler, replyClient, answerGenerator, _) = CreateHandler();
        answerGenerator.ResponseToReturn = "## 開學日\n**8/31** 星期一\n- 帶文具\n- 帶課本";

        await handler.HandleEventAsync(TextMessageFrom(AdminUserId, "開學日是幾號?"));

        Assert.Equal(1, replyClient.CallCount);
        Assert.DoesNotContain("##", replyClient.LastText);
        Assert.DoesNotContain("**", replyClient.LastText);
        Assert.Contains("開學日", replyClient.LastText);
        Assert.Contains("• 帶文具", replyClient.LastText);
    }

    [Fact]
    public async Task HandleEventAsync_UserAddedViaDbStore_TreatedAsAllowed_RepliesWithGeneratedAnswer()
    {
        var (handler, replyClient, answerGenerator, _) = CreateHandler();
        answerGenerator.ResponseToReturn = "開學是8/31喔";

        await handler.HandleEventAsync(TextMessageFrom(DbAllowedUserId, "開學日是幾號?"));

        Assert.Equal(1, replyClient.CallCount);
        Assert.Equal("開學是8/31喔", replyClient.LastText);
    }

    [Fact]
    public async Task HandleEventAsync_DisallowedUser_RepliesWithTheirOwnUserIdButNeverCallsTheAnswerGenerator()
    {
        // Without this, a family member has no way to find their own LINE User ID to
        // give the admin — confirmed as a real onboarding gap during live testing.
        var (handler, replyClient, answerGenerator, _) = CreateHandler();

        await handler.HandleEventAsync(TextMessageFrom(DisallowedUserId, "開學日是幾號?"));

        Assert.Equal(1, replyClient.CallCount);
        Assert.Contains(DisallowedUserId, replyClient.LastText);
        Assert.Equal(0, answerGenerator.CallCount);
    }

    [Fact]
    public async Task HandleEventAsync_AdminSendsAddUserCommand_ValidId_AddsToStoreAndConfirms()
    {
        var (handler, replyClient, answerGenerator, store) = CreateHandler();

        await handler.HandleEventAsync(TextMessageFrom(AdminUserId, $"#AddUser={NewUserToAddId}"));

        Assert.Contains(NewUserToAddId, store.Users);
        Assert.Equal(AdminUserId, store.LastAddedBy);
        Assert.Equal(1, replyClient.CallCount);
        Assert.Contains(NewUserToAddId, replyClient.LastText);
        Assert.Equal(0, answerGenerator.CallCount); // never treated as a question
    }

    [Fact]
    public async Task HandleEventAsync_AdminSendsAddUserCommand_AlreadyAdded_RepliesWithoutError()
    {
        var (handler, replyClient, _, store) = CreateHandler();
        store.Users.Add(NewUserToAddId);

        await handler.HandleEventAsync(TextMessageFrom(AdminUserId, $"#AddUser={NewUserToAddId}"));

        Assert.Equal(1, replyClient.CallCount);
        Assert.Contains(NewUserToAddId, replyClient.LastText);
    }

    [Fact]
    public async Task HandleEventAsync_AdminSendsAddUserCommand_MalformedId_RepliesWithFormatErrorAndDoesNotAdd()
    {
        var (handler, replyClient, _, store) = CreateHandler();

        await handler.HandleEventAsync(TextMessageFrom(AdminUserId, "#AddUser=not-a-real-id"));

        Assert.DoesNotContain("not-a-real-id", store.Users);
        Assert.Equal(1, replyClient.CallCount);
        Assert.Contains("not-a-real-id", replyClient.LastText);
    }

    [Fact]
    public async Task HandleEventAsync_NonAdminSendsAddUserLookingText_TreatedAsAnOrdinaryQuestion()
    {
        var (handler, replyClient, answerGenerator, store) = CreateHandler();
        // DbAllowedUserId is allowed to ask questions, but is not an admin — the
        // "#AddUser=" prefix must only be special-cased for admins.
        answerGenerator.ResponseToReturn = "answer";

        await handler.HandleEventAsync(TextMessageFrom(DbAllowedUserId, $"#AddUser={NewUserToAddId}"));

        Assert.DoesNotContain(NewUserToAddId, store.Users);
        Assert.Equal(1, answerGenerator.CallCount);
        Assert.Contains($"#AddUser={NewUserToAddId}", answerGenerator.LastPrompt);
        Assert.Equal("answer", replyClient.LastText);
    }

    [Fact]
    public async Task HandleEventAsync_NonMessageEvent_IsIgnored()
    {
        var (handler, replyClient, _, _) = CreateHandler();
        var lineEvent = new LineEvent
        {
            Type = "follow",
            Source = new LineSource { Type = "user", UserId = AdminUserId },
            ReplyToken = "reply-token-abc",
        };

        await handler.HandleEventAsync(lineEvent);

        Assert.Equal(0, replyClient.CallCount);
    }

    [Fact]
    public async Task HandleEventAsync_NonTextMessage_IsIgnored()
    {
        var (handler, replyClient, _, _) = CreateHandler();
        var lineEvent = new LineEvent
        {
            Type = "message",
            Message = new LineMessage { Type = "sticker" },
            Source = new LineSource { Type = "user", UserId = AdminUserId },
            ReplyToken = "reply-token-abc",
        };

        await handler.HandleEventAsync(lineEvent);

        Assert.Equal(0, replyClient.CallCount);
    }

    [Fact]
    public async Task HandleEventAsync_MissingUserId_NeverReplies()
    {
        var (handler, replyClient, _, _) = CreateHandler();
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
