using KorrnellHelper.Application.Ai;
using KorrnellHelper.Infrastructure.Ai;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace KorrnellHelper.Api.HealthChecks;

public sealed class GeminiEmbeddingHealthCheck(
    IEmbeddingGenerator embeddingGenerator,
    IOptions<GeminiOptions> geminiOptions)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var embedding = await embeddingGenerator.EmbedAsync("korrnellHelper health check", cancellationToken);
            var expectedDimension = geminiOptions.Value.EmbeddingDimension;

            if (embedding.Length != expectedDimension)
            {
                return HealthCheckResult.Degraded(
                    $"Gemini returned a {embedding.Length}-dimension vector, expected {expectedDimension}.");
            }

            return HealthCheckResult.Healthy($"Gemini embedContent succeeded, dimension {embedding.Length}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Gemini embedContent call failed.", ex);
        }
    }
}
