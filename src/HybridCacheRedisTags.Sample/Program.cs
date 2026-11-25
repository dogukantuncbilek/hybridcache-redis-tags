using HybridCacheRedisTags.Sample;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var redisConnectionString = builder.Configuration["Redis:ConnectionString"];
var healthChecks = builder.Services.AddHealthChecks();

if (string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddSingleton<ITagInvalidator, LocalTagInvalidator>();
}
else
{
    builder.Services.AddSingleton<RedisTagInvalidator>();
    builder.Services.AddSingleton<ITagInvalidator>(sp => sp.GetRequiredService<RedisTagInvalidator>());
    builder.Services.AddHostedService(sp => sp.GetRequiredService<RedisTagInvalidator>());
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.InstanceName = builder.Configuration["Redis:InstanceName"] ?? "hybridcache-sample:";
    });
    builder.Services.AddOptions<RedisCacheOptions>().Configure<IConnectionMultiplexer>((options, multiplexer) =>
        options.ConnectionMultiplexerFactory = () => Task.FromResult(multiplexer));
    healthChecks.AddCheck<RedisHealthCheck>("redis", failureStatus: HealthStatus.Unhealthy, tags: ["ready"]);
}

builder.Services.AddHybridCache(options =>
{
    options.MaximumPayloadBytes = 1024 * 1024;
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(1)
    };
});

builder.Services.AddSingleton<ProductRepository>();
builder.Services.AddSingleton<ProductCatalogCache>();

var app = builder.Build();

app.MapGet("/", () => Results.Redirect("/products/1"));

app.MapGet("/products/{id:int}", async (int id, ProductCatalogCache catalog, CancellationToken ct) =>
    await catalog.GetProductAsync(id, ct) is { } product ? Results.Ok(product) : Results.NotFound());

app.MapGet("/categories/{category}/products", async (string category, ProductCatalogCache catalog, CancellationToken ct) =>
    Results.Ok(await catalog.GetByCategoryAsync(category, ct)));

app.MapPut("/products/{id:int}", async (int id, [FromBody] ProductUpdate update, ProductCatalogCache catalog, CancellationToken ct) =>
    await catalog.UpdateAsync(id, update, ct) is { } product ? Results.Ok(product) : Results.NotFound());

app.MapDelete("/products/{id:int}", async (int id, ProductCatalogCache catalog, CancellationToken ct) =>
    await catalog.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound());

app.MapPost("/cache/invalidate/{tag}", async (string tag, ProductCatalogCache catalog, CancellationToken ct) =>
{
    await catalog.InvalidateTagAsync(tag, ct);
    return Results.Accepted(value: new { tag });
});

app.MapGet("/stats", (ProductRepository repository) => Results.Ok(new
{
    repositoryReads = repository.Reads,
    distributedCache = string.IsNullOrWhiteSpace(redisConnectionString) ? "memory" : "redis"
}));

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();

public partial class Program;
