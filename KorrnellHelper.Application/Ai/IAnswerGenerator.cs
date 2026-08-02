namespace KorrnellHelper.Application.Ai;

/// <summary>
/// Produces a natural-language answer from a question and its retrieved context.
/// </summary>
public interface IAnswerGenerator
{
    Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default);
}
