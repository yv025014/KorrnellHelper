using System.Text;
using System.Text.RegularExpressions;
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

        return new AnswerQuestionResult(StripMarkdownArtifacts(answer), usedChunks);
    }

    private static string BuildPrompt(string question, IReadOnlyList<DocumentChunk> chunks)
    {
        var context = new StringBuilder();
        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            context.AppendLine(
                chunk.Heading is not null
                    ? $"[參考資料 {i + 1}](主題:{chunk.Heading})"
                    : $"[參考資料 {i + 1}]");
            context.AppendLine(chunk.Content);
            context.AppendLine();
        }

        return $"""
            你是「康乃爾小幫手」,協助家長理解學校發出的通知單內容。請只根據下方提供的參考資料回答問題,不要編造參考資料中沒有的資訊;如果參考資料不足以回答,請明確說明無法從現有資料中找到答案,而不是用其他知識作答。

            這則回答會直接顯示在 LINE 聊天室裡,LINE 不支援 Markdown,所以絕對不要使用任何 Markdown 語法(例如 #、##、**、__、- 清單符號)。請改用適度換行分段、數字表情符號(1️⃣2️⃣3️⃣...)列步驟、⚠️ 標示重要提醒,讓內容在 LINE 上清楚好讀。

            參考資料:
            {context}

            家長的問題:{question}
            """;
    }

    /// <summary>
    /// Gemini doesn't always fully obey the "no Markdown" instruction in the prompt, so this
    /// strips whatever slips through — otherwise the raw syntax shows up as literal garbage
    /// characters in LINE, which renders plain text only.
    /// </summary>
    private static string StripMarkdownArtifacts(string text)
    {
        text = Regex.Replace(text, @"^#{1,6}[ \t]*", string.Empty, RegexOptions.Multiline);
        text = Regex.Replace(text, @"\*\*(.+?)\*\*", "$1");
        text = Regex.Replace(text, @"__(.+?)__", "$1");
        text = Regex.Replace(text, @"^[ \t]*[-*][ \t]+", "• ", RegexOptions.Multiline);
        text = Regex.Replace(text, @"(?<!\*)\*(?!\*)([^*\n]+?)\*(?!\*)", "$1");
        text = Regex.Replace(text, @"(?<!_)_(?!_)([^_\n]+?)_(?!_)", "$1");
        return text;
    }
}
