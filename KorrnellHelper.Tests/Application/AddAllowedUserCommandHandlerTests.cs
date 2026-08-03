using KorrnellHelper.Application.Line;
using KorrnellHelper.Tests.TestSupport;
using Xunit;

namespace KorrnellHelper.Tests.Application;

public class AddAllowedUserCommandHandlerTests
{
    private const string ValidUserId = "Uabcdef0123456789abcdef0123456789";
    private const string AdminId = "Uadmin-for-test-purposes-only"; // AddedBy isn't format-validated by the handler

    [Fact]
    public async Task HandleAsync_ValidNewUser_ReturnsAddedAndStoresWhoAddedThem()
    {
        var store = new FakeAllowedUserStore();
        var handler = new AddAllowedUserCommandHandler(store);

        var result = await handler.HandleAsync(new AddAllowedUserCommand(ValidUserId, AdminId));

        Assert.Equal(AddAllowedUserOutcome.Added, result.Outcome);
        Assert.Contains(ValidUserId, store.Users);
        Assert.Equal(AdminId, store.LastAddedBy);
    }

    [Fact]
    public async Task HandleAsync_AlreadyExists_ReturnsAlreadyExistsWithoutError()
    {
        var store = new FakeAllowedUserStore();
        store.Users.Add(ValidUserId);
        var handler = new AddAllowedUserCommandHandler(store);

        var result = await handler.HandleAsync(new AddAllowedUserCommand(ValidUserId, AdminId));

        Assert.Equal(AddAllowedUserOutcome.AlreadyExists, result.Outcome);
    }

    [Theory]
    [InlineData("not-a-line-id")]
    [InlineData("U802d80fd4dd0697d859be7ac49aaa5f")] // one hex char short
    [InlineData("")]
    public async Task HandleAsync_InvalidFormat_ReturnsInvalidFormatWithoutTouchingTheStore(string malformedId)
    {
        var store = new FakeAllowedUserStore();
        var handler = new AddAllowedUserCommandHandler(store);

        var result = await handler.HandleAsync(new AddAllowedUserCommand(malformedId, AdminId));

        Assert.Equal(AddAllowedUserOutcome.InvalidFormat, result.Outcome);
        Assert.Empty(store.Users);
    }
}
