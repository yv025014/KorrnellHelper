using System.Text.RegularExpressions;

namespace KorrnellHelper.Api.Line;

/// <summary>
/// Converts a generated answer into LINE-friendly plain text. LINE renders plain text only,
/// so any Markdown syntax the model produces (headings, bold, list markers, ...) would
/// otherwise show up as literal garbage characters in the chat.
/// </summary>
public static class LineAnswerFormatter
{
    public static string Format(string text)
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
