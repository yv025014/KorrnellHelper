using System.Net.Http.Json;
using KorrnellHelper.Application.Ai;
using Microsoft.Extensions.Options;

namespace KorrnellHelper.Infrastructure.Ai;

/// <summary>
/// Calls the Gemini Developer API's embedContent/generateContent REST endpoints directly.
/// HttpClient's BaseAddress is expected to be "https://generativelanguage.googleapis.com/v1beta/".
/// </summary>
public sealed class GeminiClient(HttpClient httpClient, IOptions<GeminiOptions> options)
    : IEmbeddingGenerator, IAnswerGenerator
{
    private readonly GeminiOptions _options = options.Value;

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var request = new EmbedContentRequest
        {
            Content = new GeminiContent { Parts = [new GeminiPart { Text = text }] },
            OutputDimensionality = _options.EmbeddingDimension,
        };

        var body = await PostAsync<EmbedContentRequest, EmbedContentResponse>(
            $"models/{_options.EmbeddingModel}:embedContent",
            request,
            cancellationToken);

        var values = body?.Embedding?.Values;
        if (values is null || values.Length == 0)
        {
            throw new InvalidOperationException("Gemini embedContent returned no embedding values.");
        }

        return values;
    }

    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var request = new GenerateContentRequest
        {
            Contents = [new GeminiContent { Parts = [new GeminiPart { Text = prompt }] }],
        };

        var body = await PostAsync<GenerateContentRequest, GenerateContentResponse>(
            $"models/{_options.GenerationModel}:generateContent",
            request,
            cancellationToken);

        var text = body?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        if (string.IsNullOrEmpty(text))
        {
            throw new InvalidOperationException("Gemini generateContent returned no candidate text.");
        }

        return text;
    }

    private async Task<TResponse?> PostAsync<TRequest, TResponse>(
        string requestUri,
        TRequest request,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(request),
        };
        // Sent as a header rather than "?key=..." in the URL so the key never ends up in
        // request-logging middleware, proxy access logs, or Cloud Run's request logs.
        message.Headers.Add("x-goog-api-key", _options.ApiKey);

        var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);
    }
}
