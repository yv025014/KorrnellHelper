using KorrnellHelper.Domain.Line;

namespace KorrnellHelper.Application.Line;

public sealed class AddAllowedUserCommandHandler(IAllowedUserStore store)
{
    public async Task<AddAllowedUserResult> HandleAsync(
        AddAllowedUserCommand command, CancellationToken cancellationToken = default)
    {
        if (!LineUserIdFormat.IsValid(command.UserId))
        {
            return new AddAllowedUserResult(AddAllowedUserOutcome.InvalidFormat);
        }

        var wasNewlyAdded = await store.AddAsync(command.UserId, command.AddedByUserId, cancellationToken);
        var outcome = wasNewlyAdded ? AddAllowedUserOutcome.Added : AddAllowedUserOutcome.AlreadyExists;

        return new AddAllowedUserResult(outcome);
    }
}
