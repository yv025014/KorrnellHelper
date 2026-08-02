using System.Text;

namespace KorrnellHelper.Domain.Chunking;

/// <summary>
/// Splits a converted notice's Markdown into one section per "## " heading —
/// the boundary the pdf-to-notice-md skill uses for each distinct topic.
/// Deeper headings (###, ####) stay nested inside their enclosing section
/// rather than becoming their own chunks, since they're still part of the
/// same topic (e.g. "學校作息規劃及請假" covers several related sub-steps).
/// </summary>
public static class MarkdownChunker
{
    private const string H1Prefix = "# ";
    private const string H2Prefix = "## ";

    public static IReadOnlyList<MarkdownSection> Split(string markdown)
    {
        var lines = (markdown ?? string.Empty).Replace("\r\n", "\n").Split('\n');

        var startIndex = 0;
        string? currentHeading = null;

        var firstNonBlank = Array.FindIndex(lines, static line => !string.IsNullOrWhiteSpace(line));
        if (firstNonBlank >= 0 && lines[firstNonBlank].StartsWith(H1Prefix, StringComparison.Ordinal))
        {
            currentHeading = lines[firstNonBlank][H1Prefix.Length..].Trim();
            startIndex = firstNonBlank + 1;
        }

        var sections = new List<MarkdownSection>();
        var buffer = new StringBuilder();

        void Flush()
        {
            var content = buffer.ToString().Trim();
            // Content must be non-empty regardless of heading — an empty section (e.g. a
            // title immediately followed by the next "## ", with no intro paragraph between
            // them) has nothing to embed, and Gemini's embedContent API rejects empty input.
            if (content.Length > 0)
            {
                sections.Add(new MarkdownSection(currentHeading, content));
            }
            buffer.Clear();
        }

        for (var i = startIndex; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.StartsWith(H2Prefix, StringComparison.Ordinal))
            {
                Flush();
                currentHeading = line[H2Prefix.Length..].Trim();
                continue;
            }

            buffer.AppendLine(line);
        }

        Flush();

        return sections;
    }
}
