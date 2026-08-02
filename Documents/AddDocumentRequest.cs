namespace KorrnellHelper.Api.Documents;

/// <summary>
/// HTTP contract for POST /api/documents — see <see cref="KorrnellHelper.Application.Documents.AddDocumentCommand"/>
/// for the field contract this maps onto.
/// </summary>
public sealed record AddDocumentRequest(
    string SourceDocument,
    string MarkdownContent,
    int? SchoolYear,
    DateOnly? PublishedDate);
