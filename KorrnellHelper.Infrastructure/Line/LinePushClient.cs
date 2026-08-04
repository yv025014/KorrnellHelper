using System.Net.Http.Headers;
using System.Net.Http.Json;
using KorrnellHelper.Application.Line;
using KorrnellHelper.Infrastructure.Http;
using Microsoft.Extensions.Options;

namespace KorrnellHelper.Infrastructure.Line;

/// <summary>
/// Calls LINE's Push Message API directly. HttpClient's BaseAddress is expected to be
/// "https://api.line.me/v2/bot/". Unlike Reply, this counts against LINE's monthly free
/// message quota per recipient.
/// </summary>
public sealed class LinePushClient(HttpClient httpClient, IOptions<LineOptions> options) : ILinePushClient
{
    // LINE rejects text messages over 5000 characters — truncate defensively so a
    // long generated digest degrades gracefully instead of silently failing to send.
    private const int MaxTextLength = 5000;

    public async Task PushAsync(string userId, string text, CancellationToken cancellationToken = default)
    {
        var truncated = text.Length > MaxTextLength ? text[..MaxTextLength] : text;

        var request = new PushRequest
        {
            To = userId,
            Messages = [new ReplyMessage { Text = truncated }],
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, "message/push")
        {
            Content = JsonContent.Create(request),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ChannelAccessToken);

        await httpClient.SendAndEnsureSuccessAsync(message, "LINE push API", cancellationToken);
    }
}
