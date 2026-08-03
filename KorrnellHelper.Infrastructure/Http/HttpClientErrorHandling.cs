namespace KorrnellHelper.Infrastructure.Http;

/// <summary>
/// Shared by GeminiClient and LineReplyClient — both are thin wrappers over a
/// third-party REST API with the same "send, and if it failed throw with the
/// response body included" shape.
/// </summary>
public static class HttpClientErrorHandling
{
    public static async Task<HttpResponseMessage> SendAndEnsureSuccessAsync(
        this HttpClient httpClient,
        HttpRequestMessage request,
        string serviceName,
        CancellationToken cancellationToken)
    {
        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"{serviceName} call to {request.RequestUri} failed with {(int)response.StatusCode} {response.StatusCode}: {errorBody}",
                inner: null,
                response.StatusCode);
        }

        return response;
    }
}
