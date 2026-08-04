namespace KorrnellHelper.Application.Reminders;

public sealed record SendReminderDigestResult(bool Succeeded, int PushedCount, IReadOnlyList<string> FailedUserIds);
