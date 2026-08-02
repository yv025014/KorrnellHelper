using KorrnellHelper.Application.Ai;
using KorrnellHelper.Domain.Chunking;
using KorrnellHelper.Domain.Documents;

namespace KorrnellHelper.Application.Documents;

public sealed class AddDocumentCommandHandler(
    IEmbeddingGenerator embeddingGenerator,
    IDocumentChunkStore store,
    TimeProvider timeProvider)
{
    public async Task<int> HandleAsync(AddDocumentCommand command, CancellationToken cancellationToken = default)
    {
        var sections = MarkdownChunker.Split(command.MarkdownContent);
        if (sections.Count == 0)
        {
            return 0;
        }

        var chunks = new List<DocumentChunk>(sections.Count);
        var now = timeProvider.GetUtcNow();

        foreach (var section in sections)
        {
            var embedding = await embeddingGenerator.EmbedAsync(section.Content, cancellationToken);

            chunks.Add(new DocumentChunk
            {
                Id = Guid.NewGuid(),
                SourceDocument = command.SourceDocument,
                Heading = section.Heading,
                Content = section.Content,
                Embedding = embedding,
                SchoolYear = command.SchoolYear,
                PublishedDate = command.PublishedDate,
                CreatedAt = now,
            });
        }

        await store.AddRangeAsync(chunks, cancellationToken);

        return chunks.Count;
    }
}
