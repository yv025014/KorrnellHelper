using System.ComponentModel.DataAnnotations;

namespace KorrnellHelper.Infrastructure.Ai;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    [Required(AllowEmptyStrings = false)]
    public required string ApiKey { get; set; }

    public string EmbeddingModel { get; set; } = "gemini-embedding-001";

    public string GenerationModel { get; set; } = "gemini-2.0-flash";

    /// <summary>
    /// Must match the "vector(N)" dimension declared in schema.sql.
    /// </summary>
    public int EmbeddingDimension { get; set; } = 768;
}
