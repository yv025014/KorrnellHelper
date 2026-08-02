using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace KorrnellHelper.Api.Security;

/// <summary>
/// Guards ingest-only endpoints (like Add Document) with a single shared API
/// key sent via the "X-Api-Key" header. There's no user identity or session
/// here on purpose — the only caller is our own upload CLI/skill.
/// </summary>
public sealed class ApiKeyAuthFilter(IOptions<IngestOptions> options) : IEndpointFilter
{
    private const string HeaderName = "X-Api-Key";

    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var provided = context.HttpContext.Request.Headers[HeaderName].ToString();
        if (!IsValid(provided, options.Value.ApiKey))
        {
            return ValueTask.FromResult<object?>(Results.Unauthorized());
        }

        return next(context);
    }

    // Public (not private) so the comparison logic can be unit-tested directly,
    // instead of only through a hand-built EndpointFilterInvocationContext.
    public static bool IsValid(string provided, string expected)
    {
        if (provided.Length == 0)
        {
            return false;
        }

        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);

        // Lengths differing is itself not secret, but comparing byte-for-byte
        // only when lengths already match keeps this a constant-time check
        // for same-length inputs, which is the case that actually matters.
        return providedBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
