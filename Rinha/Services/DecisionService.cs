using System.Collections.Concurrent;
using System.Text.Json;
using Rinha.Models;
using StackExchange.Redis;

namespace Rinha.Services;

public class DecisionService
{
    private readonly PaymentHealthCheckService _healthCheckService;
    private readonly ILogger<DecisionService> _logger;
    private readonly IDatabase _redis;
    
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromSeconds(5);
    private const int LatencyThreshold = 1000; // 1000ms threshold
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Local lock to prevent multiple threads from fetching the same health check simultaneously
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _fetchLocks = new();

    public DecisionService(PaymentHealthCheckService healthCheckService, ILogger<DecisionService> logger, IConnectionMultiplexer redis)
    {
        _healthCheckService = healthCheckService;
        _logger = logger;
        _redis = redis.GetDatabase();
    }

    /// <summary>
    /// Decides which payment processor to use based on health checks and latency.
    /// Returns true for default processor, false for fallback processor.
    /// Optimized for speed - fails fast to meet p99 < 11ms target.
    /// </summary>
    public async Task<bool> DecidePaymentProcessor()
    {
        // Single attempt only - no retries for speed
        // Check default processor health first (lower fees)
        var defaultHealth = await GetCachedHealthCheckAsync("default", _healthCheckService.GetDefaultProcessorHealthAsync);
        
        // If default is healthy and fast, use it immediately
        if (defaultHealth != null && !defaultHealth.Failing && defaultHealth.MinResponseTime < LatencyThreshold)
        {
            _logger.LogDebug("DECISION_RESULT: Using default processor - healthy and low latency ({Latency}ms)", defaultHealth.MinResponseTime);
            return true;
        }
        
        // Check fallback only if default is problematic
        var fallbackHealth = await GetCachedHealthCheckAsync("fallback", _healthCheckService.GetFallbackProcessorHealthAsync);
        
        // Use fallback if it's healthy (regardless of default state for speed)
        if (fallbackHealth != null && !fallbackHealth.Failing)
        {
            _logger.LogDebug("DECISION_RESULT: Using fallback processor - default unavailable or slow");
            return false;
        }
        
        // Both problematic or unavailable - default to primary (lower fees)
        _logger.LogDebug("DECISION_RESULT: Both processors problematic - defaulting to primary (lower fees)");
        return true;
    }

    private async Task<PaymentProcessorHealthCheck?> GetCachedHealthCheckAsync(
        string processorKey, 
        Func<Task<PaymentProcessorHealthCheck?>> healthCheckFunc)
    {
        var cacheKey = $"health_check:{processorKey}";
        
        try
        {
            // Try to get from Redis cache first
            var cachedData = await _redis.StringGetAsync(cacheKey);
            if (cachedData.HasValue)
            {
                var healthCheck = JsonSerializer.Deserialize<PaymentProcessorHealthCheck>(cachedData!, JsonOptions);
                return healthCheck;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read health check from Redis cache for {ProcessorKey}", processorKey);
        }

        // Get or create a semaphore for this processor to prevent multiple simultaneous fetches
        var semaphore = _fetchLocks.GetOrAdd(processorKey, _ => new SemaphoreSlim(1, 1));
        
        await semaphore.WaitAsync();
        try
        {
            // Double-check cache after acquiring lock (another thread might have fetched it)
            try
            {
                var cachedData = await _redis.StringGetAsync(cacheKey);
                if (cachedData.HasValue)
                {
                    var healthCheck = JsonSerializer.Deserialize<PaymentProcessorHealthCheck>(cachedData!, JsonOptions);
                    return healthCheck;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read health check from Redis cache during double-check for {ProcessorKey}", processorKey);
            }

            // Fetch fresh health check
            var health = await healthCheckFunc();
            
            // Cache the result in Redis with expiration
            if (health != null)
            {
                try
                {
                    var jsonData = JsonSerializer.Serialize(health, JsonOptions);
                    await _redis.StringSetAsync(cacheKey, jsonData, CacheExpiry);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cache health check in Redis for {ProcessorKey}", processorKey);
                }
            }

            return health;
        }
        finally
        {
            semaphore.Release();
        }
    }
}
