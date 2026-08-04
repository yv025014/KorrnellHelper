namespace KorrnellHelper.Application.Line;

/// <summary>
/// Sends an unsolicited message to a specific LINE user — distinct from
/// replying to an inbound message, this counts against LINE's monthly free
/// message quota per recipient.
/// </summary>
public interface ILinePushClient
{
    Task PushAsync(string userId, string text, CancellationToken cancellationToken = default);
}
