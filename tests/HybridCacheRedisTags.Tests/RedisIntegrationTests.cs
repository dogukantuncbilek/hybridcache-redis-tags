using System.Net;
using System.Net.Http.Json;
using HybridCacheRedisTags.Sample;

namespace HybridCacheRedisTags.Tests;

public sealed class RedisIntegrationTests
{
    private static string? Redis => SampleAppFactory.RedisFromEnvironment;

    [Fact]
    public async Task Second_instance_is_served_from_redis_after_the_first_populated_it()
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

        Assert.Equal(fromA!.Version, fromB!.Version);
        Assert.Equal(1, database.Reads);
    }

    [Fact]
    public async Task Tag_invalidation_on_one_instance_reaches_the_other_through_pub_sub()
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

        await clientA.GetFromJsonAsync<Product>("/products/2");
        var beforeOnB = await clientB.GetFromJsonAsync<Product>("/products/2");
        await clientA.PutAsJsonAsync("/products/2", new ProductUpdate("Brake disc v2", null, null));

        Product? afterOnB = null;
        for (var attempt = 0; attempt < 40 && afterOnB?.Version != 2; attempt++)
        {
            await Task.Delay(50);
            afterOnB = await clientB.GetFromJsonAsync<Product>("/products/2");
        }

        Assert.Equal(1, beforeOnB!.Version);
        Assert.Equal(2, afterOnB!.Version);
        Assert.Equal("Brake disc v2", afterOnB.Name);
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
