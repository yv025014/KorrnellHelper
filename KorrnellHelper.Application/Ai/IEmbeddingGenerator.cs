namespace KorrnellHelper.Application.Ai;

/// <summary>
/// Converts text into a vector embedding for similarity search.
/// </summary>
public interface IEmbeddingGenerator
{
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);
}
