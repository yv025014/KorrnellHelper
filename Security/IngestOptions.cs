using System.ComponentModel.DataAnnotations;

namespace KorrnellHelper.Api.Security;

/// <summary>
/// Despite the name (kept to avoid breaking the existing "Ingest:ApiKey" user-secret),
/// this single shared key now gates every programmatic endpoint on this API — both
/// Add Document and Answer Question — not just ingestion.
/// </summary>
public sealed class IngestOptions
{
    public const string SectionName = "Ingest";

    [Required(AllowEmptyStrings = false)]
    public required string ApiKey { get; set; }
}
