using KorrnellHelper.Application.Documents;
using KorrnellHelper.Domain.Documents;
using Npgsql;
using Pgvector;

namespace KorrnellHelper.Infrastructure.Persistence;

public sealed class DocumentChunkStore(NpgsqlDataSource dataSource) : IDocumentChunkStore
{
    public async Task AddRangeAsync(IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        if (chunks.Count == 0)
        {
            return;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var chunk in chunks)
        {
            await using var command = new NpgsqlCommand(
                """
                insert into document_chunks
                    (id, source_document, heading, content, embedding, school_year, published_date, created_at)
                values
                    ($1, $2, $3, $4, $5, $6, $7, $8)
                """,
                connection,
                (NpgsqlTransaction)transaction);

            command.Parameters.AddWithValue(chunk.Id);
            command.Parameters.AddWithValue(chunk.SourceDocument);
            command.Parameters.AddWithValue((object?)chunk.Heading ?? DBNull.Value);
            command.Parameters.AddWithValue(chunk.Content);
            command.Parameters.AddWithValue(new Vector(chunk.Embedding));
            command.Parameters.AddWithValue((object?)chunk.SchoolYear ?? DBNull.Value);
            command.Parameters.AddWithValue((object?)chunk.PublishedDate ?? DBNull.Value);
            command.Parameters.AddWithValue(chunk.CreatedAt);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
