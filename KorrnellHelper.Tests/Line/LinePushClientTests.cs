using System.Net;
using System.Text.Json;
using KorrnellHelper.Infrastructure.Line;
using KorrnellHelper.Tests.TestSupport;
using Microsoft.Extensions.Options;
using Xunit;

namespace KorrnellHelper.Tests.Line;

public class LinePushClientTests
{
    private static LinePushClient CreateClient(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.line.me/v2/bot/"),
        };
        var options = Options.Create(new LineOptions
        {
            ChannelSecret = "secret",
            ChannelAccessToken = "test-access-token",
        });
        return new LinePushClient(httpClient, options);
    }

    [Fact]
    public async Task PushAsync_SendsUserIdAndTextToThePushEndpoint()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
        var client = CreateClient(handler);

        await client.PushAsync("U1234567890abcdef1234567890abcdef", "近期活動提醒");

        Assert.Contains("message/push", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal("test-access-token", handler.LastRequest.Headers.Authorization!.Parameter);

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("U1234567890abcdef1234567890abcdef", body.RootElement.GetProperty("to").GetString());
        Assert.Equal("近期活動提醒", body.RootElement.GetProperty("messages")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task PushAsync_TruncatesTextOverLinesFiveThousandCharacterLimit()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
        var client = CreateClient(handler);
        var longText = new string('字', 6000);

        await client.PushAsync("U1234567890abcdef1234567890abcdef", longText);

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        var sentText = body.RootElement.GetProperty("messages")[0].GetProperty("text").GetString();
        Assert.Equal(longText[..5000], sentText);
    }

    [Fact]
    public async Task PushAsync_NonSuccessStatusCode_ThrowsWithResponseBodyIncluded()
    {
        var handler = new FakeHttpMessageHandler(
            HttpStatusCode.Forbidden,
            """{ "message": "The user hasn't added the bot as a friend (or has blocked the bot)." }""");
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.PushAsync("U1234567890abcdef1234567890abcdef", "text"));

        Assert.Contains("403", exception.Message);
        Assert.Contains("hasn't added the bot", exception.Message);
    }
}
