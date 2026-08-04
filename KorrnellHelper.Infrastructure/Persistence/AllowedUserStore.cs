using KorrnellHelper.Application.Line;
using Npgsql;

namespace KorrnellHelper.Infrastructure.Persistence;

public sealed class AllowedUserStore(NpgsqlDataSource dataSource) : IAllowedUserStore
{
    public async Task<bool> IsAllowedAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "select exists(select 1 from allowed_line_users where line_user_id = $1)", connection);
        command.Parameters.AddWithValue(userId);

        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task<bool> AddAsync(string userId, string addedByUserId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            insert into allowed_line_users (line_user_id, added_by)
            values ($1, $2)
            on conflict (line_user_id) do nothing
            """,
            connection);
        command.Parameters.AddWithValue(userId);
        command.Parameters.AddWithValue(addedByUserId);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        return rowsAffected == 1;
    }

    public async Task<bool> RemoveAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "delete from allowed_line_users where line_user_id = $1", connection);
        command.Parameters.AddWithValue(userId);

        var rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
        return rowsAffected == 1;
    }

    public async Task<IReadOnlyList<string>> GetAllUserIdsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("select line_user_id from allowed_line_users", connection);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(reader.GetFieldValue<string>(0));
        }

        return results;
    }
}
