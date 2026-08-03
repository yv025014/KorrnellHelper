using System.Net.Http.Headers;
using System.Net.Http.Json;
using KorrnellHelper.Infrastructure.Http;
using Microsoft.Extensions.Options;

namespace KorrnellHelper.Infrastructure.Line;

/// <summary>
/// Calls LINE's Reply Message API directly. HttpClient's BaseAddress is
/// expected to be "https://api.line.me/v2/bot/".
/// </summary>
public sealed class LineReplyClient(HttpClient httpClient, IOptions<LineOptions> options) : ILineReplyClient
{
    // LINE rejects text messages over 5000 characters — truncate defensively so a
    // long generated answer degrades gracefully instead of silently failing to send.
    private const int MaxTextLength = 5000;

    public async Task ReplyAsync(string replyToken, string text, CancellationToken cancellationToken = default)
    {
        var truncated = text.Length > MaxTextLength ? text[..MaxTextLength] : text;

        var request = new ReplyRequest
        {
            ReplyToken = replyToken,
            Messages = [new ReplyMessage { Text = truncated }],
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "message/reply")
        {
            Content = JsonContent.Create(request),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ChannelAccessToken);

        await httpClient.SendAndEnsureSuccessAsync(message, "LINE reply API", cancellationToken);
    }
}
