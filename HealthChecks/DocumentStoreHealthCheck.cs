using KorrnellHelper.Infrastructure.Ai;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Npgsql;
using Pgvector;

namespace KorrnellHelper.Api.HealthChecks;

/// <summary>
/// Proves both read and write against the real document_chunks table, not just
/// connectivity — a plain "SELECT 1" ping wouldn't catch a permissions problem
/// on the table itself.
/// </summary>
public sealed class DocumentStoreHealthCheck(NpgsqlDataSource dataSource, IOptions<GeminiOptions> geminiOptions)
    : IHealthCheck
{
    private const string HealthCheckSourceDocument = "__healthcheck__";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

            var placeholderEmbedding = new Vector(new float[geminiOptions.Value.EmbeddingDimension]);

            Guid insertedId;
            await using (var insert = new NpgsqlCommand(
                """
                insert into document_chunks (source_document, heading, content, embedding)
                values ($1, 'health-check', 'health-check', $2)
                returning id
                """,
                connection))
            {
                insert.Parameters.AddWithValue(HealthCheckSourceDocument);
                insert.Parameters.AddWithValue(placeholderEmbedding);
                insertedId = (Guid)(await insert.ExecuteScalarAsync(cancellationToken))!;
            }

            await using (var select = new NpgsqlCommand(
                "select count(*) from document_chunks where id = $1", connection))
            {
                select.Parameters.AddWithValue(insertedId);
                var count = (long)(await select.ExecuteScalarAsync(cancellationToken))!;
                if (count != 1)
                {
                    return HealthCheckResult.Unhealthy("Wrote a row but couldn't read it back.");
                }
            }

            await using (var delete = new NpgsqlCommand(
                "delete from document_chunks where id = $1", connection))
            {
                delete.Parameters.AddWithValue(insertedId);
                await delete.ExecuteNonQueryAsync(cancellationToken);
            }

            return HealthCheckResult.Healthy("Insert, read-back, and delete against document_chunks all succeeded.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Supabase read/write check failed.", ex);
        }
    }
}
