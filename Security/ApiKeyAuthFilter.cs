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
        if (!ConstantTimeCompare.Equals(provided, options.Value.ApiKey))
        {
            return ValueTask.FromResult<object?>(Results.Unauthorized());
        }

        return next(context);
    }
}
