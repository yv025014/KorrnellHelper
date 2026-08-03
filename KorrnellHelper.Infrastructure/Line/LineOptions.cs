using System.ComponentModel.DataAnnotations;

namespace KorrnellHelper.Infrastructure.Line;

public sealed class LineOptions
{
    public const string SectionName = "Line";

    [Required(AllowEmptyStrings = false)]
    public required string ChannelSecret { get; set; }

    [Required(AllowEmptyStrings = false)]
    public required string ChannelAccessToken { get; set; }

    /// <summary>
    /// Comma-separated LINE user IDs allowed to talk to the bot — set via an
    /// environment variable/user-secret, never hardcoded. Empty by default,
    /// which means nobody is authorized until this is explicitly configured.
    /// </summary>
    public string AllowedUserIds { get; set; } = string.Empty;

    private HashSet<string>? _allowedUserIdSet;

    public bool IsUserAllowed(string userId)
    {
        _allowedUserIdSet ??= AllowedUserIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

        return _allowedUserIdSet.Contains(userId);
    }
}
