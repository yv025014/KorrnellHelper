namespace KorrnellHelper.Application.Documents;

/// <summary>
/// MarkdownContent is expected to already have any YAML frontmatter stripped —
/// school_year/published_date travel as their own fields here instead.
/// </summary>
public sealed record AddDocumentCommand(
    string SourceDocument,
    string MarkdownContent,
    int? SchoolYear,
    DateOnly? PublishedDate);
