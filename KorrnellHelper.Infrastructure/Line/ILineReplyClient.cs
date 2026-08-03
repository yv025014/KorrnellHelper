namespace KorrnellHelper.Infrastructure.Line;

public interface ILineReplyClient
{
    Task ReplyAsync(string replyToken, string text, CancellationToken cancellationToken = default);
}
