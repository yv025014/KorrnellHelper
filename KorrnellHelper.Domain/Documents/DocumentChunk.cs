namespace KorrnellHelper.Domain.Documents;

/// <summary>
/// One retrievable unit of a school notice — one MarkdownSection, embedded.
/// </summary>
public sealed class DocumentChunk
{
    public required Guid Id { get; init; }

    public required string SourceDocument { get; init; }

    public string? Heading { get; init; }

    public required string Content { get; init; }

    public required float[] Embedding { get; init; }

    public int? SchoolYear { get; init; }

    public DateOnly? PublishedDate { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
