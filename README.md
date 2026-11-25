# hybridcache-redis-tags

[![ci](https://github.com/dogukantuncbilek/hybridcache-redis-tags/actions/workflows/ci.yml/badge.svg)](https://github.com/dogukantuncbilek/hybridcache-redis-tags/actions/workflows/ci.yml)

A small, runnable answer to "how do I invalidate related cache entries across several app instances?" with .NET 10 `HybridCache` and Redis.

`HybridCache` gives you an in-process L1, a distributed L2 (Redis here), stampede protection and **tags**. Tags are the part most samples skip: every entry is written with the tags it belongs to, and one `RemoveByTagAsync("category:brakes")` drops every entry carrying that tag on every instance, without anyone keeping a list of keys.

## What the sample does

A product catalogue backed by a fake repository with simulated latency and a read counter, so you can see cache behaviour in numbers.

| Endpoint | Cache behaviour |
|---|---|
| `GET /products/{id}` | cached 10 min (L1 1 min), tags `products`, `product:{id}` |
| `GET /categories/{name}/products` | cached 5 min (L1 30 s), tags `products`, `category:{name}` |
| `PUT /products/{id}` | updates, then invalidates `product:{id}` and the old (and new) `category:*` tag |
| `DELETE /products/{id}` | removes, invalidates the same tags |
| `POST /cache/invalidate/{tag}` | manual tag invalidation, e.g. `products` to flush everything |
| `GET /stats` | repository read count and which L2 is in use |
| `GET /health`, `GET /health/ready` | readiness includes a Redis PING check when Redis is configured |

Without `Redis:ConnectionString` the app falls back to an in-memory distributed cache, so it runs and its tests pass with nothing installed.

## Run

Two instances sharing one Redis:

```bash
docker compose up --build
curl -s localhost:8081/products/1          # instance A: miss, repository read
curl -s localhost:8082/products/1          # instance B: hit from Redis, no repository read
curl -s -X PUT localhost:8081/products/1 -H 'content-type: application/json' -d '{"name":"Brake pads XL"}'
curl -s localhost:8082/products/1          # instance B sees the new version: tag invalidation crossed instances
curl -s localhost:8081/stats
```

Locally without Docker:

```bash
dotnet run --project src/HybridCacheRedisTags.Sample
```

Point it at a Redis with `Redis__ConnectionString=localhost:6379` (environment variable) or `appsettings.json`.

## Tests

```bash
dotnet test                       # in-memory L2: caching, tag invalidation, stampede, health
REDIS_URL=localhost:6379 dotnet test   # adds the two-instance Redis tests
```

What they prove:

- the second read of a product does not touch the repository;
- updating a product invalidates its entry and its category list, and nothing else;
- moving a product between categories invalidates both lists;
- `POST /cache/invalidate/products` drops every entry;
- 25 concurrent misses for the same key call the repository once;
- with Redis: instance B is served from L2 after instance A populated it, and sees A's invalidation within milliseconds through the pub/sub bridge.

## The parts worth copying

- `ProductCatalogCache.cs`: the whole caching surface in one class. State is passed into `GetOrCreateAsync` through a tuple and a `static` lambda, so the factory does not capture anything per call.
- `CacheTags.cs` / `CacheKeys.cs`: keys and tags built in one place, lower-cased, so an invalidation never misses because of a typo.
- `Program.cs`: Redis is optional; the `IConnectionMultiplexer` is registered once and handed to `RedisCacheOptions.ConnectionMultiplexerFactory`, so the cache, the health check and the pub/sub bridge share a connection.
- `TagInvalidation.cs`: `ITagInvalidator` with a local implementation and the Redis pub/sub implementation that makes tag invalidation reach every instance immediately.
- `RedisHealthCheck.cs`: PING with a latency threshold, tagged `ready`, so `/health/ready` fails before traffic hits a dead Redis.

## Why the pub/sub bridge exists

`RemoveByTagAsync` is timestamp-based: it records "everything carrying this tag and written before now is stale", locally and in L2, and entries are checked lazily on read, so invalidating a tag is O(1) no matter how many keys carry it. What it does **not** do is reach into the L1 of other instances: a node that already holds a value keeps serving it until its `LocalCacheExpiration` runs out (the first version of the two-instance test in this repo failed exactly that way).

`RedisTagInvalidator` closes the gap: every invalidation is applied locally and published on a Redis channel; every instance subscribes and applies invalidations from other origins to its own cache. Without Redis the app uses `LocalTagInvalidator`, which is just `RemoveByTagAsync`. The interface `ITagInvalidator` is the only thing the catalogue code sees.

## Notes

- `MaximumPayloadBytes` is set to 1 MB; larger values are not cached, which is the behaviour you want for accidental giant responses.
- `LocalCacheExpiration` shorter than `Expiration` bounds how long an instance can serve a value that another instance has already replaced without tags, for example when a write path forgets to invalidate.

## License

MIT
