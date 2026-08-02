using Npgsql;

namespace KorrnellHelper.Infrastructure.Persistence;

/// <summary>
/// Supabase's dashboard hands out connection strings in libpq URI form
/// ("postgresql://user:pass@host:port/db"), but Npgsql's own connection
/// string parser only understands the ADO.NET "Host=...;Port=...;..." form.
/// This converts the former into the latter so either form can be stored in
/// configuration without the caller needing to know which one it is.
/// </summary>
public static class PostgresConnectionStringFactory
{
    public static string Normalize(string rawConnectionString)
    {
        var isUriForm =
            rawConnectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            rawConnectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);

        if (!isUriForm)
        {
            return rawConnectionString;
        }

        var uri = new Uri(rawConnectionString);
        var userInfoParts = uri.UserInfo.Split(':', 2);
        var username = Uri.UnescapeDataString(userInfoParts[0]);
        var password = userInfoParts.Length > 1 ? Uri.UnescapeDataString(userInfoParts[1]) : string.Empty;
        var database = uri.AbsolutePath.TrimStart('/');
        var port = uri.Port == -1 ? 5432 : uri.Port;

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = port,
            Username = username,
            Password = password,
            Database = database,
            SslMode = SslMode.Require,
        };

        return builder.ConnectionString;
    }
}
