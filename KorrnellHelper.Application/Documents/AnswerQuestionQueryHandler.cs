using KorrnellHelper.Application.Ai;
using KorrnellHelper.Domain.Documents;

namespace KorrnellHelper.Application.Documents;

public sealed class AnswerQuestionQueryHandler(
    IEmbeddingGenerator embeddingGenerator,
    IDocumentChunkSearcher searcher,
    IAnswerGenerator answerGenerator)
{
    /// <summary>How many raw vector-search candidates to fetch before de-duplicating by topic.</summary>
    private const int CandidateLimit = 8;

    /// <summary>How many de-duplicated chunks to actually hand to the generation model as context.</summary>
    private const int ContextLimit = 5;

    private const string NoDataFallbackAnswer = "目前的資料庫裡還沒有能回答這個問題的通知單內容,建議直接確認學校最新公告。";

    public async Task<AnswerQuestionResult> HandleAsync(
        AnswerQuestionQuery query, CancellationToken cancellationToken = default)
    {
        var questionEmbedding = await embeddingGenerator.EmbedAsync(query.Question, cancellationToken);
        var candidates = await searcher.SearchAsync(questionEmbedding, CandidateLimit, cancellationToken);
        var usedChunks = ChunkRanker.SelectMostRecentPerTopic(candidates).Take(ContextLimit).ToList();

        if (usedChunks.Count == 0)
        {
            return new AnswerQuestionResult(NoDataFallbackAnswer, usedChunks);
        }

        var prompt = BuildPrompt(query.Question, usedChunks);
        var answer = await answerGenerator.GenerateAsync(prompt, cancellationToken);

        return new AnswerQuestionResult(answer, usedChunks);
    }

    private static string BuildPrompt(string question, IReadOnlyList<DocumentChunk> chunks)
    {
        var context = DocumentChunkPromptFormatter.FormatAsContext(chunks);

        return $"""
            你是「康乃爾小幫手」,協助家長理解學校發出的通知單內容。請只根據下方提供的參考資料回答問題,不要編造參考資料中沒有的資訊;如果參考資料不足以回答,請明確說明無法從現有資料中找到答案,而不是用其他知識作答。

            參考資料:
            {context}

            家長的問題:{question}
            """;
    }
}
