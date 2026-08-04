using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KorrnellHelper.Api.Line;
using KorrnellHelper.Infrastructure.Line;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KorrnellHelper.Tests.Line;

public class LineWebhookDispatcherTests
{
    private const string ChannelSecret = "test-channel-secret";

    private sealed class FakeLineWebhookHandler : ILineWebhookHandler
    {
        public List<LineEvent> HandledEvents { get; } = [];

        /// <summary>Event types (matched by ReplyToken) that should throw when handled.</summary>
        public HashSet<string> ReplyTokensToThrowFor { get; } = [];

        public Task HandleEventAsync(LineEvent lineEvent, CancellationToken cancellationToken = default)
        {
            HandledEvents.Add(lineEvent);
            if (lineEvent.ReplyToken is not null && ReplyTokensToThrowFor.Contains(lineEvent.ReplyToken))
            {
                throw new InvalidOperationException("simulated failure handling this event");
            }

            return Task.CompletedTask;
        }
    }

    private static LineWebhookDispatcher CreateDispatcher(FakeLineWebhookHandler handler)
    {
        var lineOptions = Options.Create(new LineOptions
        {
            ChannelSecret = ChannelSecret,
            ChannelAccessToken = "token",
        });

        return new LineWebhookDispatcher(handler, lineOptions, NullLogger<LineWebhookDispatcher>.Instance);
    }

    private static string ComputeSignature(string rawBody, string channelSecret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(channelSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        return Convert.ToBase64String(hash);
    }

    private static string BodyWithEvents(params string[] replyTokens)
    {
        var payload = new
        {
            events = replyTokens.Select(token => new
            {
                type = "message",
                message = new { type = "text", text = "問題" },
                source = new { type = "user", userId = "Uabcdef0123456789abcdef0123456789" },
                replyToken = token,
            }),
        };
        return JsonSerializer.Serialize(payload);
    }

    [Fact]
    public async Task DispatchAsync_InvalidSignature_ReturnsFalseAndNeverCallsHandler()
    {
        var handler = new FakeLineWebhookHandler();
        var dispatcher = CreateDispatcher(handler);
        var body = BodyWithEvents("token-1");

        var isValid = await dispatcher.DispatchAsync(body, "not-a-real-signature");

        Assert.False(isValid);
        Assert.Empty(handler.HandledEvents);
    }

    [Fact]
    public async Task DispatchAsync_ValidSignature_CallsHandlerOnceForEachEvent_InOrder()
    {
        var handler = new FakeLineWebhookHandler();
        var dispatcher = CreateDispatcher(handler);
        var body = BodyWithEvents("token-1", "token-2", "token-3");
        var signature = ComputeSignature(body, ChannelSecret);

        var isValid = await dispatcher.DispatchAsync(body, signature);

        Assert.True(isValid);
        Assert.Equal(["token-1", "token-2", "token-3"], handler.HandledEvents.Select(e => e.ReplyToken));
    }

    [Fact]
    public async Task DispatchAsync_OneEventThrows_OtherEventsInTheBatchAreStillDispatched()
    {
        var handler = new FakeLineWebhookHandler();
        handler.ReplyTokensToThrowFor.Add("token-2");
        var dispatcher = CreateDispatcher(handler);
        var body = BodyWithEvents("token-1", "token-2", "token-3");
        var signature = ComputeSignature(body, ChannelSecret);

        var isValid = await dispatcher.DispatchAsync(body, signature);

        Assert.True(isValid);
        Assert.Equal(["token-1", "token-2", "token-3"], handler.HandledEvents.Select(e => e.ReplyToken));
    }

    [Fact]
    public async Task DispatchAsync_MalformedJsonBody_ValidSignature_DoesNotThrow()
    {
        var handler = new FakeLineWebhookHandler();
        var dispatcher = CreateDispatcher(handler);
        const string body = "{ this is not valid json";
        var signature = ComputeSignature(body, ChannelSecret);

        var isValid = await dispatcher.DispatchAsync(body, signature);

        Assert.True(isValid);
        Assert.Empty(handler.HandledEvents);
    }

    [Fact]
    public async Task DispatchAsync_NoEvents_ReturnsTrueWithoutCallingHandler()
    {
        var handler = new FakeLineWebhookHandler();
        var dispatcher = CreateDispatcher(handler);
        var body = BodyWithEvents();
        var signature = ComputeSignature(body, ChannelSecret);

        var isValid = await dispatcher.DispatchAsync(body, signature);

        Assert.True(isValid);
        Assert.Empty(handler.HandledEvents);
    }
}
