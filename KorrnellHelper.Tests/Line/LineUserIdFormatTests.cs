using KorrnellHelper.Domain.Line;
using Xunit;

namespace KorrnellHelper.Tests.Line;

public class LineUserIdFormatTests
{
    [Theory]
    [InlineData("U802d80fd4dd0697d859be7ac49aaa5f2")] // a real ID seen in this project's own logs
    [InlineData("Uabcdef0123456789abcdef0123456789")] // U + 32 hex chars
    public void IsValid_WellFormedId_ReturnsTrue(string userId)
    {
        Assert.True(LineUserIdFormat.IsValid(userId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("U802d80fd4dd0697d859be7ac49aaa5f")] // 31 hex chars, one short
    [InlineData("U802d80fd4dd0697d859be7ac49aaa5f22")] // 33 hex chars, one too many
    [InlineData("802d80fd4dd0697d859be7ac49aaa5f2")] // missing leading U
    [InlineData("u802d80fd4dd0697d859be7ac49aaa5f2")] // lowercase u
    [InlineData("U802D80FD4DD0697D859BE7AC49AAA5F2")] // uppercase hex
    [InlineData("U802d80fd4dd0697d859be7ac49aaa5g2")] // 'g' is not hex
    [InlineData("not-a-line-id")]
    public void IsValid_MalformedId_ReturnsFalse(string userId)
    {
        Assert.False(LineUserIdFormat.IsValid(userId));
    }
}
