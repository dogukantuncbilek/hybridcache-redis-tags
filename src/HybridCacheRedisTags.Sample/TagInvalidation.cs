using System.Text.Json;
using Microsoft.Extensions.Caching.Hybrid;
using StackExchange.Redis;

namespace HybridCacheRedisTags.Sample;

public interface ITagInvalidator
{
    ValueTask InvalidateAsync(string tag, CancellationToken ct = default);
}

public sealed class LocalTagInvalidator(HybridCache cache) : ITagInvalidator
{
    public ValueTask InvalidateAsync(string tag, CancellationToken ct = default) => cache.RemoveByTagAsync(tag, ct);
}

public sealed record TagInvalidationMessage(string Tag, string Origin);

public sealed class RedisTagInvalidator(HybridCache cache, IConnectionMultiplexer connection, ILogger<RedisTagInvalidator> logger)
    : ITagInvalidator, IHostedService
{
    public const string Channel = "hybridcache:tag-invalidations";

    private readonly string _origin = Guid.NewGuid().ToString("N");
    private readonly RedisChannel _channel = RedisChannel.Literal(Channel);

    public async ValueTask InvalidateAsync(string tag, CancellationToken ct = default)
    {
        await cache.RemoveByTagAsync(tag, ct);
        var payload = JsonSerializer.Serialize(new TagInvalidationMessage(tag, _origin));
        await connection.GetSubscriber().PublishAsync(_channel, payload);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await connection.GetSubscriber().SubscribeAsync(_channel, OnMessage);
    }

    public Task StopAsync(CancellationToken cancellationToken) => connection.GetSubscriber().UnsubscribeAsync(_channel, OnMessage);

    private async void OnMessage(RedisChannel _, RedisValue value)
    {
        try
        {
            var message = JsonSerializer.Deserialize<TagInvalidationMessage>(value.ToString());
            if (message is null || message.Origin == _origin)
            {
                return;
            }

            await cache.RemoveByTagAsync(message.Tag);
            logger.LogDebug("Invalidated tag {Tag} on request from {Origin}", message.Tag, message.Origin);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to apply remote tag invalidation");
        }
    }
}
