namespace KorrnellHelper.Application.Line;

public interface IAllowedUserStore
{
    Task<bool> IsAllowedAsync(string userId, CancellationToken cancellationToken = default);

    /// <returns>true if newly added, false if the user was already in the store.</returns>
    Task<bool> AddAsync(string userId, string addedByUserId, CancellationToken cancellationToken = default);

    /// <returns>true if removed, false if the user was not in the store.</returns>
    Task<bool> RemoveAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Every LINE User ID currently in the whitelist — the scheduled reminder's recipient list.</summary>
    Task<IReadOnlyList<string>> GetAllUserIdsAsync(CancellationToken cancellationToken = default);
}
