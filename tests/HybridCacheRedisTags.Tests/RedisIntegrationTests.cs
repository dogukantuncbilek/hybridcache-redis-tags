using System.Net;
using System.Net.Http.Json;
using HybridCacheRedisTags.Sample;

namespace HybridCacheRedisTags.Tests;

public sealed class RedisIntegrationTests
{
    private static string? Redis => SampleAppFactory.RedisFromEnvironment;

    [Fact]
    public async Task Second_instance_reads_from_redis_and_sees_tag_invalidation_from_the_first()
    {
        if (Redis is null)
        {
            return;
        }

        var database = new ProductRepository(TimeSpan.Zero);
        var instance = $"shared-{Guid.NewGuid():N}:";
        using var first = new SampleAppFactory(Redis, database, instance);
        using var second = new SampleAppFactory(Redis, database, instance);
        using var clientA = first.CreateClient();
        using var clientB = second.CreateClient();

        var fromA = await clientA.GetFromJsonAsync<Product>("/products/1");
        var fromB = await clientB.GetFromJsonAsync<Product>("/products/1");
        var readsAfterBothRead = database.Reads;

        await clientA.PutAsJsonAsync("/products/1", new ProductUpdate("Brake pads v2", null, null));
        var fromBAfter = await clientB.GetFromJsonAsync<Product>("/products/1");

        Assert.Equal(1, fromA!.Version);
        Assert.Equal(1, fromB!.Version);
        Assert.Equal(1, readsAfterBothRead);
        Assert.Equal(2, fromBAfter!.Version);
    }

    [Fact]
    public async Task Readiness_reports_redis()
    {
        if (Redis is null)
        {
            return;
        }

        using var factory = new SampleAppFactory(Redis, null, null);
        using var client = factory.CreateClient();

        var ready = await client.GetAsync("/health/ready");
        var stats = await client.GetFromJsonAsync<Dictionary<string, object>>("/stats");

        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal("redis", stats!["distributedCache"].ToString());
    }
}
