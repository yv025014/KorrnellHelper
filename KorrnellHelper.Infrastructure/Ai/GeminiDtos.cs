using System.Text.Json.Serialization;

namespace KorrnellHelper.Infrastructure.Ai;

internal sealed class EmbedContentRequest
{
    [JsonPropertyName("content")]
    public required GeminiContent Content { get; init; }

    [JsonPropertyName("outputDimensionality")]
    public required int OutputDimensionality { get; init; }
}

internal sealed class EmbedContentResponse
{
    [JsonPropertyName("embedding")]
    public GeminiEmbedding? Embedding { get; init; }
}

internal sealed class GeminiEmbedding
{
    [JsonPropertyName("values")]
    public float[]? Values { get; init; }
}

internal sealed class GenerateContentRequest
{
    [JsonPropertyName("contents")]
    public required GeminiContent[] Contents { get; init; }
}

internal sealed class GenerateContentResponse
{
    [JsonPropertyName("candidates")]
    public GeminiCandidate[]? Candidates { get; init; }
}

internal sealed class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; init; }
}

internal sealed class GeminiContent
{
    [JsonPropertyName("parts")]
    public required GeminiPart[] Parts { get; init; }
}

internal sealed class GeminiPart
{
    [JsonPropertyName("text")]
    public required string Text { get; init; }
}
