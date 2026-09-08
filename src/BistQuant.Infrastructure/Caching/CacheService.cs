using System.Text.Json;
using BistQuant.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace BistQuant.Infrastructure.Caching;

public class CacheService : ICacheService
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<CacheService> _logger;
    private readonly IDatabase? _db;

    public CacheService(
        IMemoryCache memoryCache,
        ILogger<CacheService> logger,
        IConnectionMultiplexer? redis = null)
    {
        _memoryCache = memoryCache;
        _logger = logger;
        _redis = redis;

        if (_redis != null && _redis.IsConnected)
        {
            try
            {
                _db = _redis.GetDatabase();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis configured but unable to obtain database. Falling back to MemoryCache.");
            }
        }
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_db != null && _redis != null && _redis.IsConnected)
        {
            try
            {
                var value = await _db.StringGetAsync(key);
                if (value.HasValue)
                {
                    return JsonSerializer.Deserialize<T>(value.ToString()!);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis GET failed for key {Key}. Falling back to MemoryCache.", key);
            }
        }

        if (_memoryCache.TryGetValue(key, out T? cachedValue))
        {
            return cachedValue;
        }

        return default;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var ttl = expiration ?? TimeSpan.FromMinutes(10);

        if (_db != null && _redis != null && _redis.IsConnected)
        {
            try
            {
                var json = JsonSerializer.Serialize(value);
                await _db.StringSetAsync(key, json, ttl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis SET failed for key {Key}. Storing in MemoryCache instead.", key);
            }
        }

        _memoryCache.Set(key, value, ttl);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_db != null && _redis != null && _redis.IsConnected)
        {
            try
            {
                await _db.KeyDeleteAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis DELETE failed for key {Key}.", key);
            }
        }

        _memoryCache.Remove(key);
    }
}
