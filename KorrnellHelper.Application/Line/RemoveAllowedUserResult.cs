namespace KorrnellHelper.Application.Line;

public enum RemoveAllowedUserOutcome
{
    Removed,
    NotFound,
    InvalidFormat,
}

public sealed record RemoveAllowedUserResult(RemoveAllowedUserOutcome Outcome);
