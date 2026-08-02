using System.ComponentModel.DataAnnotations;

namespace KorrnellHelper.Infrastructure.Ai;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    [Required(AllowEmptyStrings = false)]
    public required string ApiKey { get; set; }

    public string EmbeddingModel { get; set; } = "gemini-embedding-001";

    // Pinned model versions get deprecated/quota-zeroed over time (confirmed live:
    // "gemini-2.0-flash" returned a hard 0-quota 429 on the free tier) — the
    // "-latest" alias tracks whatever the current recommended flash model is.
    public string GenerationModel { get; set; } = "gemini-flash-latest";

    /// <summary>
    /// Must match the "vector(N)" dimension declared in schema.sql.
    /// </summary>
    public int EmbeddingDimension { get; set; } = 768;
}
