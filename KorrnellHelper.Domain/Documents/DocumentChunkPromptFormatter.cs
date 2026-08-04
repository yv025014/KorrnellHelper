using System.Text;

namespace KorrnellHelper.Domain.Documents;

/// <summary>
/// Renders chunks as the numbered "[參考資料 N](主題:...)" reference blocks shared by every
/// prompt that grounds a generation call in retrieved document content.
/// </summary>
public static class DocumentChunkPromptFormatter
{
    public static string FormatAsContext(IReadOnlyList<DocumentChunk> chunks)
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

        return context.ToString();
    }
}
