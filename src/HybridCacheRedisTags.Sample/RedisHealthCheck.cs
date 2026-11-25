using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace HybridCacheRedisTags.Sample;

public sealed class RedisHealthCheck(IConnectionMultiplexer connection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var latency = await connection.GetDatabase().PingAsync();
            return latency < TimeSpan.FromSeconds(1)
                ? HealthCheckResult.Healthy($"ping {latency.TotalMilliseconds:F1} ms")
                : HealthCheckResult.Degraded($"ping {latency.TotalMilliseconds:F1} ms");
        }
        catch (RedisException ex)
        {
            return HealthCheckResult.Unhealthy("redis unreachable", ex);
        }
    }
}
