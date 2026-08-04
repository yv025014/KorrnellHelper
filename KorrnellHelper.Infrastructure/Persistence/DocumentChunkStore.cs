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

    public async Task<IReadOnlyList<DocumentChunk>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            select id, source_document, heading, content, embedding, school_year, published_date, created_at
            from document_chunks
            """,
            connection);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<DocumentChunk>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new DocumentChunk
            {
                Id = reader.GetFieldValue<Guid>(0),
                SourceDocument = reader.GetFieldValue<string>(1),
                Heading = reader.IsDBNull(2) ? null : reader.GetFieldValue<string>(2),
                Content = reader.GetFieldValue<string>(3),
                Embedding = reader.GetFieldValue<Vector>(4).ToArray(),
                SchoolYear = reader.IsDBNull(5) ? null : reader.GetFieldValue<int>(5),
                PublishedDate = reader.IsDBNull(6) ? null : reader.GetFieldValue<DateOnly>(6),
                CreatedAt = reader.GetFieldValue<DateTimeOffset>(7),
            });
        }

        return results;
    }
}
