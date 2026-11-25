namespace HybridCacheRedisTags.Sample;

public sealed record Product(int Id, string Name, string Category, decimal Price, int Version);

public sealed record ProductUpdate(string? Name, string? Category, decimal? Price);
