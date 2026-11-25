using System.Collections.Concurrent;

namespace HybridCacheRedisTags.Sample;

public sealed class ProductRepository
{
    private readonly ConcurrentDictionary<int, Product> _rows = new(Seed().ToDictionary(p => p.Id));
    private readonly TimeSpan _latency;
    private int _reads;

    public ProductRepository(TimeSpan? latency = null)
    {
        _latency = latency ?? TimeSpan.FromMilliseconds(150);
    }

    public int Reads => Volatile.Read(ref _reads);

    public async Task<Product?> FindAsync(int id, CancellationToken ct)
    {
        await SimulateIo(ct);
        return _rows.GetValueOrDefault(id);
    }

    public async Task<IReadOnlyList<Product>> ByCategoryAsync(string category, CancellationToken ct)
    {
        await SimulateIo(ct);
        return _rows.Values
            .Where(p => string.Equals(p.Category, category, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Id)
            .ToList();
    }

    public Product? Update(int id, ProductUpdate update)
    {
        Product? updated = null;
        _rows.AddOrUpdate(
            id,
            _ => throw new KeyNotFoundException(),
            (_, current) => updated = current with
            {
                Name = update.Name ?? current.Name,
                Category = update.Category ?? current.Category,
                Price = update.Price ?? current.Price,
                Version = current.Version + 1
            });
        return updated;
    }

    public bool Remove(int id) => _rows.TryRemove(id, out _);

    private async Task SimulateIo(CancellationToken ct)
    {
        Interlocked.Increment(ref _reads);
        if (_latency > TimeSpan.Zero)
        {
            await Task.Delay(_latency, ct);
        }
    }

    private static IEnumerable<Product> Seed() =>
    [
        new(1, "Brake pads", "brakes", 42.50m, 1),
        new(2, "Brake disc", "brakes", 89.00m, 1),
        new(3, "Oil filter", "engine", 12.90m, 1),
        new(4, "Air filter", "engine", 18.40m, 1),
        new(5, "Wiper blade", "exterior", 9.99m, 1)
    ];
}
