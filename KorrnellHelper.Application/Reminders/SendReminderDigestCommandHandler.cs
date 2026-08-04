using KorrnellHelper.Application.Line;

namespace KorrnellHelper.Application.Reminders;

/// <summary>
/// Pushes the reminder digest to a list of recipients, one Push call per recipient rather than
/// one Multicast call for the whole list — LINE's Multicast API fails the entire request if any
/// single target is invalid or has blocked the account, which would silently deny everyone else
/// too. Pushing individually means a single bad recipient only costs that one recipient.
/// </summary>
public sealed class SendReminderDigestCommandHandler(
    GenerateReminderDigestQueryHandler digestHandler,
    ILinePushClient pushClient)
{
    public async Task<SendReminderDigestResult> HandleAsync(
        SendReminderDigestCommand command, CancellationToken cancellationToken = default)
    {
        GenerateReminderDigestResult digest;
        try
        {
            digest = await digestHandler.HandleAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await TryAlertAdminAsync(
                command.AdminUserId, $"提醒排程執行失敗（摘要生成錯誤）：{ex.Message}", cancellationToken);
            return new SendReminderDigestResult(false, 0, []);
        }

        if (!digest.HasActivities)
        {
            return new SendReminderDigestResult(true, 0, []);
        }

        var failedUserIds = new List<string>();
        foreach (var userId in command.RecipientUserIds)
        {
            try
            {
                await pushClient.PushAsync(userId, digest.DigestText, cancellationToken);
            }
            catch
            {
                failedUserIds.Add(userId);
            }
        }

        if (failedUserIds.Count > 0)
        {
            var failureMessage =
                $"提醒排程部分發送失敗：以下收件人未成功收到推播，可能已封鎖官方帳號：\n{string.Join("\n", failedUserIds)}";
            await TryAlertAdminAsync(command.AdminUserId, failureMessage, cancellationToken);
        }

        var pushedCount = command.RecipientUserIds.Count - failedUserIds.Count;
        return new SendReminderDigestResult(failedUserIds.Count == 0, pushedCount, failedUserIds);
    }

    private async Task TryAlertAdminAsync(string adminUserId, string message, CancellationToken cancellationToken)
    {
        try
        {
            await pushClient.PushAsync(adminUserId, message, cancellationToken);
        }
        catch
        {
            // Best-effort — if even the admin alert fails to send, there's nothing further this
            // handler can do about it; the caller sees the failure via the returned result.
        }
    }
}
