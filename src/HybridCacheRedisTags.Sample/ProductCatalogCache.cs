using Microsoft.Extensions.Caching.Hybrid;

namespace HybridCacheRedisTags.Sample;

public sealed class ProductCatalogCache(HybridCache cache, ProductRepository repository)
{
    private static readonly HybridCacheEntryOptions ProductOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(1)
    };

    private static readonly HybridCacheEntryOptions ListOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromSeconds(30)
    };

    public ValueTask<Product?> GetProductAsync(int id, CancellationToken ct = default) =>
        cache.GetOrCreateAsync(
            CacheKeys.Product(id),
            (repository, id),
            static (state, token) => new ValueTask<Product?>(state.repository.FindAsync(state.id, token)),
            ProductOptions,
            [CacheTags.AllProducts, CacheTags.Product(id)],
            ct);

    public ValueTask<IReadOnlyList<Product>> GetByCategoryAsync(string category, CancellationToken ct = default) =>
        cache.GetOrCreateAsync(
            CacheKeys.Category(category),
            (repository, category),
            static (state, token) => new ValueTask<IReadOnlyList<Product>>(state.repository.ByCategoryAsync(state.category, token)),
            ListOptions,
            [CacheTags.AllProducts, CacheTags.Category(category)],
            ct);

    public async Task<Product?> UpdateAsync(int id, ProductUpdate update, CancellationToken ct = default)
    {
        var before = await repository.FindAsync(id, ct);
        if (before is null)
        {
            return null;
        }

        var after = repository.Update(id, update);
        await InvalidateProductAsync(before, ct);
        if (after is not null && !string.Equals(after.Category, before.Category, StringComparison.OrdinalIgnoreCase))
        {
            await cache.RemoveByTagAsync(CacheTags.Category(after.Category), ct);
        }

        return after;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var existing = await repository.FindAsync(id, ct);
        if (existing is null || !repository.Remove(id))
        {
            return false;
        }

        await InvalidateProductAsync(existing, ct);
        return true;
    }

    public ValueTask InvalidateTagAsync(string tag, CancellationToken ct = default) => cache.RemoveByTagAsync(tag, ct);

    private async Task InvalidateProductAsync(Product product, CancellationToken ct)
    {
        await cache.RemoveByTagAsync(CacheTags.Product(product.Id), ct);
        await cache.RemoveByTagAsync(CacheTags.Category(product.Category), ct);
    }
}
