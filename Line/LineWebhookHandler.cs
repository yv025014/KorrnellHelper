using KorrnellHelper.Application.Documents;
using KorrnellHelper.Infrastructure.Line;
using Microsoft.Extensions.Options;

namespace KorrnellHelper.Api.Line;

public sealed class LineWebhookHandler(
    AnswerQuestionQueryHandler answerHandler,
    ILineReplyClient replyClient,
    IOptions<LineOptions> lineOptions,
    ILogger<LineWebhookHandler> logger)
{
    public async Task HandleEventAsync(LineEvent lineEvent, CancellationToken cancellationToken = default)
    {
        if (lineEvent.Type != "message" || lineEvent.Message?.Type != "text")
        {
            return;
        }

        var userId = lineEvent.Source?.UserId;
        if (userId is null)
        {
            return;
        }

        // Logged regardless of whitelist outcome — this is how you find your own
        // LINE User ID to add to the whitelist in the first place.
        logger.LogInformation("Received LINE message from user {UserId}", userId);

        if (lineEvent.ReplyToken is null)
        {
            return;
        }

        if (!lineOptions.Value.IsUserAllowed(userId))
        {
            logger.LogInformation("Denying message from non-whitelisted user {UserId}", userId);
            // Reply with their own ID (not silence) — otherwise there's no way for someone
            // other than the admin to find their own ID and ask to be added to the whitelist.
            await replyClient.ReplyAsync(
                lineEvent.ReplyToken,
                $"很抱歉，這個小幫手目前僅限受邀請的使用者使用。\n\n如需申請使用權限，請將以下 ID 提供給管理員：\n{userId}",
                cancellationToken);
            return;
        }

        var question = lineEvent.Message.Text;
        if (string.IsNullOrWhiteSpace(question))
        {
            return;
        }

        var result = await answerHandler.HandleAsync(new AnswerQuestionQuery(question), cancellationToken);
        await replyClient.ReplyAsync(lineEvent.ReplyToken, result.Answer, cancellationToken);
    }
}
