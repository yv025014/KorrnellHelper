using System.Text.Json;
using KorrnellHelper.Api.Security;
using KorrnellHelper.Infrastructure.Line;
using Microsoft.Extensions.Options;

namespace KorrnellHelper.Api.Line;

/// <summary>
/// Owns everything about handling one LINE webhook HTTP request: verifying the signature,
/// parsing the payload, and dispatching each event in the batch. Each event gets its own
/// fresh, generously-bounded timeout rather than sharing the inbound HTTP request's
/// cancellation — confirmed live that tying event processing to the request's own token lets
/// a slow reply race the client giving up on the connection and silently drop the reply. One
/// event's failure is logged and never stops the rest of the batch from being processed,
/// so a transient error doesn't make LINE retry-resend events that already succeeded.
/// </summary>
public sealed class LineWebhookDispatcher(
    ILineWebhookHandler handler,
    IOptions<LineOptions> lineOptions,
    ILogger<LineWebhookDispatcher> logger)
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(30);

    /// <returns>Whether the signature was valid. <c>false</c> means the payload was never parsed or dispatched.</returns>
    /// <remarks>
    /// Deliberately takes no <see cref="CancellationToken"/> for the batch itself: each event
    /// gets its own fresh, independent timeout below rather than inheriting the inbound HTTP
    /// request's cancellation (see class remarks for why), so there is nothing here that a
    /// caller-supplied token would actually bound.
    /// </remarks>
    public async Task<bool> DispatchAsync(string rawBody, string signatureHeader)
    {
        if (!LineSignatureVerifier.IsValid(rawBody, signatureHeader, lineOptions.Value.ChannelSecret))
        {
            return false;
        }

        LineWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<LineWebhookPayload>(rawBody);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to parse LINE webhook payload.");
            return true;
        }

        foreach (var lineEvent in payload?.Events ?? [])
        {
            try
            {
                using var eventTimeout = new CancellationTokenSource(EventTimeout);
                await handler.HandleEventAsync(lineEvent, eventTimeout.Token);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to handle a LINE event.");
            }
        }

        return true;
    }
}
