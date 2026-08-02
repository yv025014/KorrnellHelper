using System.Net;
using KorrnellHelper.Infrastructure.Ai;
using Microsoft.Extensions.Options;
using Xunit;

namespace KorrnellHelper.Tests.Ai;

public class GeminiClientTests
{
    private static GeminiClient CreateClient(FakeHttpMessageHandler handler, out HttpClient httpClient)
    {
        httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/"),
        };
        var options = Options.Create(new GeminiOptions
        {
            ApiKey = "test-key",
            EmbeddingModel = "gemini-embedding-001",
            GenerationModel = "gemini-2.0-flash",
            EmbeddingDimension = 3,
        });
        return new GeminiClient(httpClient, options);
    }

    [Fact]
    public async Task EmbedAsync_ParsesValuesFromResponse()
    {
        var handler = new FakeHttpMessageHandler(
            HttpStatusCode.OK,
            """{ "embedding": { "values": [0.1, 0.2, 0.3] } }""");
        var client = CreateClient(handler, out _);

        var result = await client.EmbedAsync("小一暑期銜接課程");

        Assert.Equal([0.1f, 0.2f, 0.3f], result);
        Assert.Contains("models/gemini-embedding-001:embedContent", handler.LastRequest!.RequestUri!.ToString());
        Assert.Contains("\"outputDimensionality\":3", handler.LastRequestBody);
        Assert.Equal("test-key", handler.LastRequest.Headers.GetValues("x-goog-api-key").Single());
        Assert.DoesNotContain("key=", handler.LastRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task EmbedAsync_ThrowsWhenResponseHasNoValues()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """{ "embedding": {} }""");
        var client = CreateClient(handler, out _);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.EmbedAsync("text"));
    }

    [Fact]
    public async Task GenerateAsync_ReturnsCandidateText()
    {
        var handler = new FakeHttpMessageHandler(
            HttpStatusCode.OK,
            """{ "candidates": [ { "content": { "parts": [ { "text": "開學日是8/31" } ] } } ] }""");
        var client = CreateClient(handler, out _);

        var result = await client.GenerateAsync("開學日是幾號?");

        Assert.Equal("開學日是8/31", result);
        Assert.Contains("models/gemini-2.0-flash:generateContent", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task GenerateAsync_ThrowsWhenNoCandidates()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """{ "candidates": [] }""");
        var client = CreateClient(handler, out _);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GenerateAsync("text"));
    }

    [Fact]
    public async Task NonSuccessStatusCode_ThrowsWithResponseBodyIncludedForDiagnosability()
    {
        var handler = new FakeHttpMessageHandler(
            HttpStatusCode.TooManyRequests,
            """{ "error": { "message": "Quota exceeded for quota metric ..." } }""");
        var client = CreateClient(handler, out _);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.EmbedAsync("text"));

        Assert.Contains("429", exception.Message);
        Assert.Contains("Quota exceeded", exception.Message);
    }
}
