using KorrnellHelper.Domain.Line;

namespace KorrnellHelper.Application.Line;

public sealed class RemoveAllowedUserCommandHandler(IAllowedUserStore store)
{
    public async Task<RemoveAllowedUserResult> HandleAsync(
        RemoveAllowedUserCommand command, CancellationToken cancellationToken = default)
    {
        if (!LineUserIdFormat.IsValid(command.UserId))
        {
            return new RemoveAllowedUserResult(RemoveAllowedUserOutcome.InvalidFormat);
        }

        var wasRemoved = await store.RemoveAsync(command.UserId, cancellationToken);
        var outcome = wasRemoved ? RemoveAllowedUserOutcome.Removed : RemoveAllowedUserOutcome.NotFound;

        return new RemoveAllowedUserResult(outcome);
    }
}
