using KorrnellHelper.Application.Line;
using KorrnellHelper.Tests.TestSupport;
using Xunit;

namespace KorrnellHelper.Tests.Application;

public class RemoveAllowedUserCommandHandlerTests
{
    private const string ValidUserId = "Uabcdef0123456789abcdef0123456789";

    [Fact]
    public async Task HandleAsync_UserInStore_RemovesThemAndReturnsRemoved()
    {
        var store = new FakeAllowedUserStore();
        store.Users.Add(ValidUserId);
        var handler = new RemoveAllowedUserCommandHandler(store);

        var result = await handler.HandleAsync(new RemoveAllowedUserCommand(ValidUserId));

        Assert.Equal(RemoveAllowedUserOutcome.Removed, result.Outcome);
        Assert.DoesNotContain(ValidUserId, store.Users);
    }

    [Fact]
    public async Task HandleAsync_UserNotInStore_ReturnsNotFoundWithoutError()
    {
        var store = new FakeAllowedUserStore();
        var handler = new RemoveAllowedUserCommandHandler(store);

        var result = await handler.HandleAsync(new RemoveAllowedUserCommand(ValidUserId));

        Assert.Equal(RemoveAllowedUserOutcome.NotFound, result.Outcome);
    }

    [Theory]
    [InlineData("not-a-line-id")]
    [InlineData("U802d80fd4dd0697d859be7ac49aaa5f")] // one hex char short
    [InlineData("")]
    public async Task HandleAsync_InvalidFormat_ReturnsInvalidFormatWithoutTouchingTheStore(string malformedId)
    {
        var store = new FakeAllowedUserStore();
        store.Users.Add(ValidUserId);
        var handler = new RemoveAllowedUserCommandHandler(store);

        var result = await handler.HandleAsync(new RemoveAllowedUserCommand(malformedId));

        Assert.Equal(RemoveAllowedUserOutcome.InvalidFormat, result.Outcome);
        Assert.Contains(ValidUserId, store.Users); // untouched — the unrelated existing entry survives
    }
}
