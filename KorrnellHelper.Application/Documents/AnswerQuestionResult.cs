using KorrnellHelper.Domain.Documents;

namespace KorrnellHelper.Application.Documents;

public sealed record AnswerQuestionResult(string Answer, IReadOnlyList<DocumentChunk> UsedChunks);
