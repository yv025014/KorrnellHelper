using System.Reflection;
using Npgsql;

namespace KorrnellHelper.Infrastructure.Persistence;

/// <summary>
/// Applies schema.sql idempotently on startup. There's no separate migration
/// tool here on purpose — the schema is a single small table, and re-running
/// "create ... if not exists" statements is cheap and safe.
/// </summary>
public static class SchemaInitializer
{
    public static async Task InitializeAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken = default)
    {
        var assembly = typeof(SchemaInitializer).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("schema.sql", StringComparison.Ordinal));

        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        var schemaSql = await reader.ReadToEndAsync(cancellationToken);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(schemaSql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
