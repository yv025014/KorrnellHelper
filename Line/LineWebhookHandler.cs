using KorrnellHelper.Application.Documents;
using KorrnellHelper.Application.Line;
using KorrnellHelper.Application.Reminders;
using KorrnellHelper.Infrastructure.Line;
using Microsoft.Extensions.Options;

namespace KorrnellHelper.Api.Line;

public sealed class LineWebhookHandler(
    AnswerQuestionQueryHandler answerHandler,
    AddAllowedUserCommandHandler addAllowedUserHandler,
    RemoveAllowedUserCommandHandler removeAllowedUserHandler,
    GenerateReminderDigestQueryHandler reminderDigestHandler,
    IAllowedUserStore allowedUserStore,
    ILineReplyClient replyClient,
    IOptions<LineOptions> lineOptions,
    ILogger<LineWebhookHandler> logger) : ILineWebhookHandler
{
    private const string AddUserCommandPrefix = "#AddUser=";
    private const string RemoveUserCommandPrefix = "#RemoveUser=";
    private const string TestReminderCommand = "#TestReminder";

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

        // Logged regardless of outcome — this is how you find your own LINE User ID to
        // give to an admin in the first place.
        logger.LogInformation("Received LINE message from user {UserId}", userId);

        if (lineEvent.ReplyToken is null)
        {
            return;
        }

        // Trimmed once up front — a stray leading/trailing space (common from mobile keyboards)
        // would otherwise silently break every exact/prefix command match below.
        var text = (lineEvent.Message.Text ?? string.Empty).Trim();
        var isAdmin = lineOptions.Value.IsAdmin(userId);

        // Only an admin's message is ever interpreted as this command — from anyone else,
        // literal "#AddUser=..." text is just an ordinary (if odd) question.
        if (isAdmin && text.StartsWith(AddUserCommandPrefix, StringComparison.Ordinal))
        {
            await HandleAddUserCommandAsync(userId, text, lineEvent.ReplyToken, cancellationToken);
            return;
        }

        if (isAdmin && text.StartsWith(RemoveUserCommandPrefix, StringComparison.Ordinal))
        {
            await HandleRemoveUserCommandAsync(text, lineEvent.ReplyToken, cancellationToken);
            return;
        }

        // Only an admin's exact "#TestReminder" is ever interpreted as this command — it runs
        // the same digest logic the scheduled push uses, but replies (never pushes) so repeated
        // testing after uploading a new notice never touches the monthly push message quota.
        if (isAdmin && text == TestReminderCommand)
        {
            await HandleTestReminderCommandAsync(lineEvent.ReplyToken, cancellationToken);
            return;
        }

        var isAllowed = isAdmin || await allowedUserStore.IsAllowedAsync(userId, cancellationToken);
        if (!isAllowed)
        {
            logger.LogInformation("Denying message from non-whitelisted user {UserId}", userId);
            // Reply with their own ID (not silence) — otherwise there's no way for someone
            // other than an admin to find their own ID and ask to be added to the whitelist.
            await SendReplyAsync(
                lineEvent.ReplyToken,
                $"很抱歉，這個小幫手目前僅限受邀請的使用者使用。\n\n如需申請使用權限，請將以下 ID 提供給管理員：\n{userId}");
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        string answer;
        try
        {
            var result = await answerHandler.HandleAsync(new AnswerQuestionQuery(text), cancellationToken);
            answer = LineAnswerFormatter.Format(result.Answer);
        }
        catch (Exception ex)
        {
            // Without this, a failure here (e.g. the AI provider rate-limiting or timing out)
            // leaves the user with a "read" message and no reply at all.
            logger.LogError(ex, "Failed to answer question from user {UserId}", userId);
            answer = "系統忙碌中，請稍後再試";
        }

        await SendReplyAsync(lineEvent.ReplyToken, answer);
    }

    // Uses its own short-lived token instead of the caller's cancellationToken: that token
    // bounds the (possibly slow) AI answer call above, and confirmed live that when it fires
    // mid-answer, reusing it here made this fallback reply fail too — the user got no message
    // at all instead of "系統忙碌中". Sending a reply is a fast, independent operation and
    // shouldn't inherit a deadline that was meant for something else.
    private async Task SendReplyAsync(string replyToken, string text)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await replyClient.ReplyAsync(replyToken, text, cts.Token);
    }

    private async Task HandleRemoveUserCommandAsync(string text, string replyToken, CancellationToken cancellationToken)
    {
        var targetUserId = text[RemoveUserCommandPrefix.Length..].Trim();
        var command = new RemoveAllowedUserCommand(targetUserId);
        var result = await removeAllowedUserHandler.HandleAsync(command, cancellationToken);

        var message = result.Outcome switch
        {
            RemoveAllowedUserOutcome.Removed =>
                $"已將 {targetUserId} 從白名單移除，對方之後傳訊息將不再被回應。",
            RemoveAllowedUserOutcome.NotFound =>
                $"{targetUserId} 原本就不在白名單裡，不需要移除。",
            RemoveAllowedUserOutcome.InvalidFormat => targetUserId.Length == 0
                ? "格式不正確，請照這個格式傳送：#RemoveUser=U開頭加32碼小寫英數字（= 後面不能是空的）。"
                : $"格式不正確，LINE User ID 應該是 U 開頭加 32 碼小寫英數字。收到的是：{targetUserId}",
            _ => throw new InvalidOperationException($"Unhandled RemoveAllowedUserOutcome: {result.Outcome}"),
        };

        await SendReplyAsync(replyToken, message);
    }

    private async Task HandleTestReminderCommandAsync(string replyToken, CancellationToken cancellationToken)
    {
        var result = await reminderDigestHandler.HandleAsync(cancellationToken);
        await SendReplyAsync(replyToken, result.DigestText);
    }

    private async Task HandleAddUserCommandAsync(
        string adminUserId, string text, string replyToken, CancellationToken cancellationToken)
    {
        var targetUserId = text[AddUserCommandPrefix.Length..].Trim();
        var command = new AddAllowedUserCommand(targetUserId, adminUserId);
        var result = await addAllowedUserHandler.HandleAsync(command, cancellationToken);

        var message = result.Outcome switch
        {
            AddAllowedUserOutcome.Added =>
                $"已將 {targetUserId} 加入白名單，對方現在可以直接傳訊息問問題了。",
            AddAllowedUserOutcome.AlreadyExists =>
                $"{targetUserId} 已經在白名單裡了，不需要重複新增。",
            AddAllowedUserOutcome.InvalidFormat => targetUserId.Length == 0
                ? "格式不正確，請照這個格式傳送：#AddUser=U開頭加32碼小寫英數字（= 後面不能是空的）。"
                : $"格式不正確，LINE User ID 應該是 U 開頭加 32 碼小寫英數字。收到的是：{targetUserId}",
            _ => throw new InvalidOperationException($"Unhandled AddAllowedUserOutcome: {result.Outcome}"),
        };

        await SendReplyAsync(replyToken, message);
    }
}
