namespace KorrnellHelper.Application.Reminders;

public sealed record SendReminderDigestCommand(IReadOnlyList<string> RecipientUserIds, string AdminUserId);
