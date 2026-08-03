using System.Net;

namespace KorrnellHelper.Tests.TestSupport;

/// <summary>
/// Returns a fixed response for every request, and records the last request
/// so tests can assert on the URL/body that was sent. Shared by any test
/// that exercises a thin HttpClient-based wrapper over a third-party API
/// (GeminiClient, LineReplyClient).
/// </summary>
public sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;
        LastRequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseBody),
        };
    }
}
