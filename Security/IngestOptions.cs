using System.ComponentModel.DataAnnotations;

namespace KorrnellHelper.Api.Security;

public sealed class IngestOptions
{
    public const string SectionName = "Ingest";

    [Required(AllowEmptyStrings = false)]
    public required string ApiKey { get; set; }
}
