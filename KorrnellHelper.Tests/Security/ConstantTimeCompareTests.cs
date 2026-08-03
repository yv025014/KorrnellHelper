using KorrnellHelper.Api.Security;
using Xunit;

namespace KorrnellHelper.Tests.Security;

public class ConstantTimeCompareTests
{
    [Fact]
    public void Equals_MatchingValue_ReturnsTrue()
    {
        Assert.True(ConstantTimeCompare.Equals("secret-key", "secret-key"));
    }

    [Fact]
    public void Equals_WrongValue_ReturnsFalse()
    {
        Assert.False(ConstantTimeCompare.Equals("wrong-key", "secret-key"));
    }

    [Fact]
    public void Equals_EmptyProvidedValue_ReturnsFalse()
    {
        Assert.False(ConstantTimeCompare.Equals("", "secret-key"));
    }

    [Fact]
    public void Equals_DifferentLengthValue_ReturnsFalseWithoutThrowing()
    {
        Assert.False(ConstantTimeCompare.Equals("short", "a-much-longer-secret-key"));
    }

    [Fact]
    public void Equals_CaseSensitive()
    {
        Assert.False(ConstantTimeCompare.Equals("Secret-Key", "secret-key"));
    }
}
