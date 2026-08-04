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
    /// Comma-separated LINE user IDs treated as admins — set via an environment
    /// variable/user-secret, never hardcoded. Empty by default, which means nobody
    /// is an admin until this is explicitly configured. Admins can always ask
    /// questions AND are the only ones who can run the "#AddUser=" command; everyone
    /// else's access is looked up from the allowed_line_users table instead (see
    /// IAllowedUserStore), so growing the whitelist doesn't require a restart.
    /// </summary>
    public string AllowedUserIds { get; set; } = string.Empty;

    private HashSet<string>? _adminUserIdSet;
    private string[]? _adminUserIdList;

    public bool IsAdmin(string userId)
    {
        _adminUserIdSet ??= AllowedUserIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

        return _adminUserIdSet.Contains(userId);
    }

    /// <summary>
    /// The admin who receives reminder-schedule failure alerts. Real usage today configures
    /// exactly one admin; if multiple are ever configured, only the first receives alerts —
    /// an accepted simplification, not a guarantee every admin is notified.
    /// </summary>
    public string? PrimaryAdminUserId
    {
        get
        {
            _adminUserIdList ??= AllowedUserIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return _adminUserIdList.Length > 0 ? _adminUserIdList[0] : null;
        }
    }
}
