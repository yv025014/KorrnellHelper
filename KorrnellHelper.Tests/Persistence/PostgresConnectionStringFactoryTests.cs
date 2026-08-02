using KorrnellHelper.Infrastructure.Persistence;
using Xunit;

namespace KorrnellHelper.Tests.Persistence;

public class PostgresConnectionStringFactoryTests
{
    [Fact]
    public void Normalize_ConvertsSupabaseUriForm_ToNpgsqlKeywordForm()
    {
        var result = PostgresConnectionStringFactory.Normalize(
            "postgresql://postgres.abcdefgh:s3cr3t@aws-1-ap-northeast-2.pooler.supabase.com:5432/postgres");

        Assert.Contains("Host=aws-1-ap-northeast-2.pooler.supabase.com", result);
        Assert.Contains("Port=5432", result);
        Assert.Contains("Username=postgres.abcdefgh", result);
        Assert.Contains("Password=s3cr3t", result);
        Assert.Contains("Database=postgres", result);
    }

    [Fact]
    public void Normalize_DecodesPercentEncodedPassword()
    {
        // "p@ss/word" percent-encoded, matching the guidance given for special characters.
        var result = PostgresConnectionStringFactory.Normalize(
            "postgresql://user:p%40ss%2Fword@host:5432/postgres");

        Assert.Contains("Password=p@ss/word", result);
    }

    [Fact]
    public void Normalize_LeavesAlreadyKeywordFormUnchanged()
    {
        const string keywordForm = "Host=localhost;Port=5432;Username=postgres;Password=pw;Database=postgres";

        var result = PostgresConnectionStringFactory.Normalize(keywordForm);

        Assert.Equal(keywordForm, result);
    }

    [Fact]
    public void Normalize_UriForm_RequestsSslByDefault()
    {
        var result = PostgresConnectionStringFactory.Normalize(
            "postgres://user:pw@host:5432/postgres");

        Assert.Contains("SSL Mode=Require", result);
    }
}
