using System.Net;
using System.Text.Json;
using KorrnellHelper.Infrastructure.Line;
using KorrnellHelper.Tests.TestSupport;
using Microsoft.Extensions.Options;
using Xunit;

namespace KorrnellHelper.Tests.Line;

public class LineReplyClientTests
{
    private static LineReplyClient CreateClient(FakeHttpMessageHandler handler)
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
        return new LineReplyClient(httpClient, options);
    }

    [Fact]
    public async Task ReplyAsync_SendsReplyTokenAndTextToTheReplyEndpoint()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
        var client = CreateClient(handler);

        await client.ReplyAsync("reply-token-abc", "開學是8/31喔");

        Assert.Contains("message/reply", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal("test-access-token", handler.LastRequest.Headers.Authorization!.Parameter);

        // Parsed rather than substring-matched — System.Text.Json escapes non-ASCII
        // characters as \uXXXX by default, so the raw Chinese text never appears
        // literally in the wire body even though it round-trips correctly.
        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal("reply-token-abc", body.RootElement.GetProperty("replyToken").GetString());
        Assert.Equal("開學是8/31喔", body.RootElement.GetProperty("messages")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task ReplyAsync_TruncatesTextOverLinesFiveThousandCharacterLimit()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
        var client = CreateClient(handler);
        var longText = new string('字', 6000);

        await client.ReplyAsync("reply-token-abc", longText);

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        var sentText = body.RootElement.GetProperty("messages")[0].GetProperty("text").GetString();
        Assert.Equal(longText[..5000], sentText);
    }

    [Fact]
    public async Task ReplyAsync_NonSuccessStatusCode_ThrowsWithResponseBodyIncluded()
    {
        var handler = new FakeHttpMessageHandler(
            HttpStatusCode.Unauthorized,
            """{ "message": "Invalid channel access token" }""");
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.ReplyAsync("reply-token-abc", "text"));

        Assert.Contains("401", exception.Message);
        Assert.Contains("Invalid channel access token", exception.Message);
    }
}
