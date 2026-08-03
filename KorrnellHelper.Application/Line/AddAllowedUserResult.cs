namespace KorrnellHelper.Application.Line;

public enum AddAllowedUserOutcome
{
    Added,
    AlreadyExists,
    InvalidFormat,
}

public sealed record AddAllowedUserResult(AddAllowedUserOutcome Outcome);
