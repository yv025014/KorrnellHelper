using System.Text.RegularExpressions;

namespace KorrnellHelper.Domain.Line;

/// <summary>
/// LINE user IDs are always "U" followed by 32 lowercase hex characters.
/// Validating this before it ever reaches a database write means a typo'd
/// #AddUser command fails fast with a clear reply instead of silently
/// writing garbage into the whitelist.
/// </summary>
public static partial class LineUserIdFormat
{
    public static bool IsValid(string userId) => Pattern().IsMatch(userId);

    [GeneratedRegex("^U[0-9a-f]{32}$")]
    private static partial Regex Pattern();
}
