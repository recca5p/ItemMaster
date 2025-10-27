using ItemMaster.Lambda.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ItemMaster.Lambda.Services;

public interface ICachedSecretService
{
    Task<string> GetSecretAsync(string secretKey, Func<Task<string>> secretRetriever);
    void ClearCache();
    Task InvalidateSecretAsync(string secretKey);
}

public class CachedSecretService : ICachedSecretService
{
    // Configuration constants
    private const string CACHE_KEY_PREFIX = "secret_";
    private const int DEFAULT_CACHE_DURATION_MINUTES = 15;
    private const int MAX_CACHE_DURATION_MINUTES = 60;

    private readonly IMemoryCache _cache;
    private readonly CacheConfiguration _cacheConfig;
    private readonly ILogger<CachedSecretService> _logger;

    public CachedSecretService(
        IMemoryCache cache,
        ILogger<CachedSecretService> logger,
        IOptions<CacheConfiguration> cacheOptions)
    {
        _cache = cache;
        _logger = logger;
        _cacheConfig = cacheOptions.Value;
    }

    public async Task<string> GetSecretAsync(string secretKey, Func<Task<string>> secretRetriever)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);
        ArgumentNullException.ThrowIfNull(secretRetriever);

        var cacheKey = GenerateCacheKey(secretKey);

        if (_cache.TryGetValue(cacheKey, out string? cachedSecret) && !string.IsNullOrEmpty(cachedSecret))
        {
            _logger.LogDebug("Secret retrieved from cache: {SecretKey}", secretKey);
            return cachedSecret;
        }

        return await FetchAndCacheSecretAsync(secretKey, cacheKey, secretRetriever);
    }

    public async Task InvalidateSecretAsync(string secretKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);

        var cacheKey = GenerateCacheKey(secretKey);
        _cache.Remove(cacheKey);

        _logger.LogDebug("Invalidated cached secret: {SecretKey}", secretKey);
        await Task.CompletedTask;
    }

    public void ClearCache()
    {
        if (_cache is MemoryCache memoryCache)
        {
            memoryCache.Clear();
            _logger.LogInformation("Secret cache cleared");
        }
    }

    private async Task<string> FetchAndCacheSecretAsync(string secretKey, string cacheKey,
        Func<Task<string>> secretRetriever)
    {
        _logger.LogDebug("Fetching secret from source: {SecretKey}", secretKey);
        var secret = await secretRetriever();

        if (!string.IsNullOrEmpty(secret))
        {
            var cacheOptions = CreateCacheOptions();
            _cache.Set(cacheKey, secret, cacheOptions);

            _logger.LogDebug("Secret cached for {Duration} minutes: {SecretKey}",
                _cacheConfig.SecretCacheDurationMinutes, secretKey);
        }

        return secret;
    }

    private static string GenerateCacheKey(string secretKey)
    {
        return $"{CACHE_KEY_PREFIX}{secretKey}";
    }

    private MemoryCacheEntryOptions CreateCacheOptions()
    {
        var cacheDuration = ValidateCacheDuration(_cacheConfig.SecretCacheDurationMinutes);

        return new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(cacheDuration),
            SlidingExpiration = TimeSpan.FromMinutes(cacheDuration / 2),
            Priority = CacheItemPriority.High
        };
    }

    private static int ValidateCacheDuration(int configuredDuration)
    {
        return configuredDuration switch
        {
            <= 0 => DEFAULT_CACHE_DURATION_MINUTES,
            > MAX_CACHE_DURATION_MINUTES => MAX_CACHE_DURATION_MINUTES,
            _ => configuredDuration
        };
    }
}