using System.Net;
using System.Net.Http.Json;
using HybridCacheRedisTags.Sample;

namespace HybridCacheRedisTags.Tests;

public sealed class TagInvalidationTests : IDisposable
{
    private readonly SampleAppFactory _factory = new();
    private readonly HttpClient _client;

    public TagInvalidationTests()
    {
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Second_read_is_served_from_cache()
    {
        await _client.GetFromJsonAsync<Product>("/products/1");
        await _client.GetFromJsonAsync<Product>("/products/1");

        Assert.Equal(1, _factory.Repository.Reads);
    }

    [Fact]
    public async Task Updating_a_product_invalidates_its_entry_and_its_category_list()
    {
        await _client.GetFromJsonAsync<Product>("/products/1");
        await _client.GetFromJsonAsync<List<Product>>("/categories/brakes/products");
        await _client.GetFromJsonAsync<Product>("/products/3");
        var readsBefore = _factory.Repository.Reads;

        var response = await _client.PutAsJsonAsync("/products/1", new ProductUpdate("Brake pads XL", null, 44.00m));
        var after = await _client.GetFromJsonAsync<Product>("/products/1");
        var list = await _client.GetFromJsonAsync<List<Product>>("/categories/brakes/products");
        await _client.GetFromJsonAsync<Product>("/products/3");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Brake pads XL", after!.Name);
        Assert.Equal(2, after.Version);
        Assert.Contains(list!, p => p.Id == 1 && p.Name == "Brake pads XL");
        Assert.Equal(readsBefore + 3, _factory.Repository.Reads);
    }

    [Fact]
    public async Task Moving_a_product_between_categories_invalidates_both_lists()
    {
        await _client.GetFromJsonAsync<List<Product>>("/categories/brakes/products");
        await _client.GetFromJsonAsync<List<Product>>("/categories/engine/products");

        await _client.PutAsJsonAsync("/products/2", new ProductUpdate(null, "engine", null));
        var brakes = await _client.GetFromJsonAsync<List<Product>>("/categories/brakes/products");
        var engine = await _client.GetFromJsonAsync<List<Product>>("/categories/engine/products");

        Assert.DoesNotContain(brakes!, p => p.Id == 2);
        Assert.Contains(engine!, p => p.Id == 2);
    }

    [Fact]
    public async Task Invalidating_the_products_tag_drops_every_entry()
    {
        await _client.GetFromJsonAsync<Product>("/products/1");
        await _client.GetFromJsonAsync<Product>("/products/2");
        await _client.GetFromJsonAsync<List<Product>>("/categories/engine/products");
        var readsBefore = _factory.Repository.Reads;

        var response = await _client.PostAsync($"/cache/invalidate/{CacheTags.AllProducts}", null);
        await _client.GetFromJsonAsync<Product>("/products/1");
        await _client.GetFromJsonAsync<Product>("/products/2");
        await _client.GetFromJsonAsync<List<Product>>("/categories/engine/products");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(readsBefore + 3, _factory.Repository.Reads);
    }

    [Fact]
    public async Task Deleting_a_product_returns_404_afterwards()
    {
        await _client.GetFromJsonAsync<Product>("/products/5");

        var delete = await _client.DeleteAsync("/products/5");
        var get = await _client.GetAsync("/products/5");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Concurrent_misses_call_the_repository_once()
    {
        var requests = Enumerable.Range(0, 25).Select(_ => _client.GetFromJsonAsync<Product>("/products/4"));

        var products = await Task.WhenAll(requests);

        Assert.All(products, p => Assert.Equal(4, p!.Id));
        Assert.Equal(1, _factory.Repository.Reads);
    }

    [Fact]
    public async Task Health_endpoint_is_healthy_without_redis()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
