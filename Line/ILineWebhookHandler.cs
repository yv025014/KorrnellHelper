namespace KorrnellHelper.Api.Line;

public interface ILineWebhookHandler
{
    Task HandleEventAsync(LineEvent lineEvent, CancellationToken cancellationToken = default);
}
