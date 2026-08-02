using KorrnellHelper.Application.Documents;
using KorrnellHelper.Domain.Documents;
using Npgsql;
using Pgvector;

namespace KorrnellHelper.Infrastructure.Persistence;

public sealed class DocumentChunkSearcher(NpgsqlDataSource dataSource) : IDocumentChunkSearcher
{
    public async Task<IReadOnlyList<DocumentChunk>> SearchAsync(
        float[] queryEmbedding, int limit, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            select id, source_document, heading, content, embedding, school_year, published_date, created_at
            from document_chunks
            order by embedding <=> $1
            limit $2
            """,
            connection);

        command.Parameters.AddWithValue(new Vector(queryEmbedding));
        command.Parameters.AddWithValue(limit);

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
