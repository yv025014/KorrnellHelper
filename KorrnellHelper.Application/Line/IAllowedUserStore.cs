namespace KorrnellHelper.Application.Line;

public interface IAllowedUserStore
{
    Task<bool> IsAllowedAsync(string userId, CancellationToken cancellationToken = default);

    /// <returns>true if newly added, false if the user was already in the store.</returns>
    Task<bool> AddAsync(string userId, string addedByUserId, CancellationToken cancellationToken = default);
}
