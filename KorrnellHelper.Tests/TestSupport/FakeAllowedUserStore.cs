using KorrnellHelper.Application.Line;

namespace KorrnellHelper.Tests.TestSupport;

/// <summary>Shared by AddAllowedUserCommandHandlerTests and LineWebhookHandlerTests.</summary>
public sealed class FakeAllowedUserStore : IAllowedUserStore
{
    public HashSet<string> Users { get; } = [];
    public string? LastAddedBy { get; private set; }

    public Task<bool> IsAllowedAsync(string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.Contains(userId));

    public Task<bool> AddAsync(string userId, string addedByUserId, CancellationToken cancellationToken = default)
    {
        LastAddedBy = addedByUserId;
        return Task.FromResult(Users.Add(userId));
    }

    public Task<bool> RemoveAsync(string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.Remove(userId));

    public Task<IReadOnlyList<string>> GetAllUserIdsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(Users.ToList());
}
