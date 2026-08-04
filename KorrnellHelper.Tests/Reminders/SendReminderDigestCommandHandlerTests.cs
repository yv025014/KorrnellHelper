using KorrnellHelper.Application.Ai;
using KorrnellHelper.Application.Line;
using KorrnellHelper.Application.Reminders;
using KorrnellHelper.Domain.Documents;
using KorrnellHelper.Tests.TestSupport;
using Xunit;

namespace KorrnellHelper.Tests.Reminders;

public class SendReminderDigestCommandHandlerTests
{
    private sealed class FakeAnswerGenerator : IAnswerGenerator
    {
        public string ResponseToReturn { get; set; } = "1. 測試活動(8/13～8/17)\n• 辦理事項：測試";
        public Exception? ExceptionToThrow { get; set; }

        public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(ResponseToReturn);
        }
    }

    private sealed class FakeLinePushClient : ILinePushClient
    {
        public List<string> PushedTo { get; } = [];
        public List<(string UserId, string Text)> Calls { get; } = [];
        public HashSet<string> UserIdsThatFail { get; } = [];

        public Task PushAsync(string userId, string text, CancellationToken cancellationToken = default)
        {
            Calls.Add((userId, text));
            if (UserIdsThatFail.Contains(userId))
            {
                throw new HttpRequestException("simulated failure: recipient blocked the official account");
            }

            PushedTo.Add(userId);
            return Task.CompletedTask;
        }
    }

    private static DocumentChunk MakeChunk() => new()
    {
        Id = Guid.NewGuid(),
        SourceDocument = "doc.md",
        Heading = "活動",
        Content = "內容",
        Embedding = [1f],
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private const string AdminUserId = "Uadmin000000000000000000000000aa";
    private const string Recipient1 = "Urecipient10000000000000000001aa";
    private const string Recipient2 = "Urecipient20000000000000000002aa";
    private const string Recipient3 = "Urecipient30000000000000000003aa";

    private static SendReminderDigestCommandHandler CreateHandler(
        FakeAnswerGenerator answerGenerator, FakeLinePushClient pushClient)
    {
        var digestHandler = new GenerateReminderDigestQueryHandler(
            new FakeDocumentChunkStore([MakeChunk()]), answerGenerator, TimeProvider.System);
        return new SendReminderDigestCommandHandler(digestHandler, pushClient);
    }

    [Fact]
    public async Task HandleAsync_AllRecipientsSucceed_PushesToEveryoneAndDoesNotAlertAdmin()
    {
        var pushClient = new FakeLinePushClient();
        var handler = CreateHandler(new FakeAnswerGenerator(), pushClient);
        var command = new SendReminderDigestCommand([Recipient1, Recipient2], AdminUserId);

        var result = await handler.HandleAsync(command);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.PushedCount);
        Assert.Empty(result.FailedUserIds);
        Assert.Contains(Recipient1, pushClient.PushedTo);
        Assert.Contains(Recipient2, pushClient.PushedTo);
        Assert.DoesNotContain(AdminUserId, pushClient.PushedTo);
    }

    [Fact]
    public async Task HandleAsync_OneRecipientFails_OthersStillReceiveAndAdminGetsNotifiedOfTheFailure()
    {
        var pushClient = new FakeLinePushClient();
        pushClient.UserIdsThatFail.Add(Recipient2);
        var handler = CreateHandler(new FakeAnswerGenerator(), pushClient);
        var command = new SendReminderDigestCommand([Recipient1, Recipient2, Recipient3], AdminUserId);

        var result = await handler.HandleAsync(command);

        Assert.False(result.Succeeded);
        Assert.Equal(2, result.PushedCount);
        Assert.Equal([Recipient2], result.FailedUserIds);
        Assert.Contains(Recipient1, pushClient.PushedTo);
        Assert.Contains(Recipient3, pushClient.PushedTo);
        Assert.DoesNotContain(Recipient2, pushClient.PushedTo);

        Assert.Contains(AdminUserId, pushClient.PushedTo);
        var adminAlert = pushClient.Calls.Last(c => c.UserId == AdminUserId);
        Assert.Contains(Recipient2, adminAlert.Text);
    }

    [Fact]
    public async Task HandleAsync_NoActivitiesInWindow_SendsNothingToAnyone()
    {
        var pushClient = new FakeLinePushClient();
        var answerGenerator = new FakeAnswerGenerator { ResponseToReturn = "NO_UPCOMING_ACTIVITIES" };
        var handler = CreateHandler(answerGenerator, pushClient);
        var command = new SendReminderDigestCommand([Recipient1, Recipient2], AdminUserId);

        var result = await handler.HandleAsync(command);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.PushedCount);
        Assert.Empty(pushClient.Calls);
    }

    [Fact]
    public async Task HandleAsync_DigestGenerationThrows_NotifiesAdminInsteadOfPropagatingException()
    {
        var pushClient = new FakeLinePushClient();
        var answerGenerator = new FakeAnswerGenerator
        {
            ExceptionToThrow = new InvalidOperationException("Gemini timed out"),
        };
        var handler = CreateHandler(answerGenerator, pushClient);
        var command = new SendReminderDigestCommand([Recipient1, Recipient2], AdminUserId);

        var result = await handler.HandleAsync(command);

        Assert.False(result.Succeeded);
        Assert.Equal(0, result.PushedCount);
        Assert.DoesNotContain(Recipient1, pushClient.PushedTo);
        Assert.DoesNotContain(Recipient2, pushClient.PushedTo);
        Assert.Contains(AdminUserId, pushClient.PushedTo);
    }
}
