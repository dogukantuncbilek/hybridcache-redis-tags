namespace HybridCacheRedisTags.Sample;

public static class CacheTags
{
    public const string AllProducts = "products";

    public static string Product(int id) => $"product:{id}";

    public static string Category(string category) => $"category:{category.ToLowerInvariant()}";
}

public static class CacheKeys
{
    public static string Product(int id) => $"product/{id}";

    public static string Category(string category) => $"category/{category.ToLowerInvariant()}/products";
}
