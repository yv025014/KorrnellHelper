using KorrnellHelper.Api.Security;
using Xunit;

namespace KorrnellHelper.Tests.Security;

public class ApiKeyAuthFilterTests
{
    [Fact]
    public void IsValid_MatchingKey_ReturnsTrue()
    {
        Assert.True(ApiKeyAuthFilter.IsValid("secret-key", "secret-key"));
    }

    [Fact]
    public void IsValid_WrongKey_ReturnsFalse()
    {
        Assert.False(ApiKeyAuthFilter.IsValid("wrong-key", "secret-key"));
    }

    [Fact]
    public void IsValid_EmptyProvidedKey_ReturnsFalse()
    {
        Assert.False(ApiKeyAuthFilter.IsValid("", "secret-key"));
    }

    [Fact]
    public void IsValid_DifferentLengthKey_ReturnsFalseWithoutThrowing()
    {
        Assert.False(ApiKeyAuthFilter.IsValid("short", "a-much-longer-secret-key"));
    }

    [Fact]
    public void IsValid_CaseSensitive()
    {
        Assert.False(ApiKeyAuthFilter.IsValid("Secret-Key", "secret-key"));
    }
}
