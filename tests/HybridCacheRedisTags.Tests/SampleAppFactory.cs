using HybridCacheRedisTags.Sample;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HybridCacheRedisTags.Tests;

public sealed class SampleAppFactory(string? redisConnectionString, ProductRepository? sharedRepository, string? instanceName)
    : WebApplicationFactory<Program>
{
    public SampleAppFactory() : this(null, null, null)
    {
    }

    public static string? RedisFromEnvironment => Environment.GetEnvironmentVariable("REDIS_URL");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Redis:ConnectionString", redisConnectionString ?? "");
        builder.UseSetting("Redis:InstanceName", instanceName ?? $"test-{Guid.NewGuid():N}:");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ProductRepository>();
            services.AddSingleton(sharedRepository ?? new ProductRepository(TimeSpan.FromMilliseconds(40)));
        });
    }

    public ProductRepository Repository => Services.GetRequiredService<ProductRepository>();
}
