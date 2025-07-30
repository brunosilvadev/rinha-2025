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
    private const int MaxRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);
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
    /// Uses circuit breaker pattern to avoid infinite retries.
    /// </summary>
    public async Task<bool> DecidePaymentProcessor()
    {
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            // Check default processor health
            var defaultHealth = await GetCachedHealthCheckAsync("default", _healthCheckService.GetDefaultProcessorHealthAsync);
            
            if (defaultHealth != null && !defaultHealth.Failing)
            {
                // Default processor is healthy, check latency
                if (defaultHealth.MinResponseTime < LatencyThreshold)
                {
                    _logger.LogDebug("Using default processor - healthy and low latency ({Latency}ms)", defaultHealth.MinResponseTime);
                    return true; // Use default processor
                }
                else
                {
                    _logger.LogInformation("Default processor latency too high ({Latency}ms), checking fallback", defaultHealth.MinResponseTime);
                    
                    // Check fallback processor health
                    var fallbackHealth = await GetCachedHealthCheckAsync("fallback", _healthCheckService.GetFallbackProcessorHealthAsync);
                    
                    if (fallbackHealth != null && !fallbackHealth.Failing)
                    {
                        // Compare latencies - only use fallback if it's actually faster
                        if (fallbackHealth.MinResponseTime < defaultHealth.MinResponseTime)
                        {
                            _logger.LogInformation("Using fallback processor - better latency ({FallbackLatency}ms vs {DefaultLatency}ms)", 
                                fallbackHealth.MinResponseTime, defaultHealth.MinResponseTime);
                            return false; // Use fallback processor
                        }
                        else
                        {
                            _logger.LogInformation("Using default processor - fallback latency not better ({FallbackLatency}ms vs {DefaultLatency}ms)", 
                                fallbackHealth.MinResponseTime, defaultHealth.MinResponseTime);
                            return true; // Use default as fallback is not faster
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Fallback processor also unhealthy, using default despite high latency");
                        return true; // Use default even with high latency if fallback is down
                    }
                }
            }
            else
            {
                _logger.LogWarning("Default processor is failing or unreachable, checking fallback");
                
                // Default processor is failing, check fallback
                var fallbackHealth = await GetCachedHealthCheckAsync("fallback", _healthCheckService.GetFallbackProcessorHealthAsync);
                
                if (fallbackHealth != null && !fallbackHealth.Failing)
                {
                    _logger.LogInformation("Using fallback processor - default is failing");
                    return false; // Use fallback processor
                }
                else
                {
                    _logger.LogWarning("Both processors are failing - attempt {Attempt}/{MaxAttempts}", attempt + 1, MaxRetries);
                    
                    // If this is the last attempt, fail fast with default processor
                    if (attempt == MaxRetries - 1)
                    {
                        _logger.LogError("Both processors are failing after {MaxAttempts} attempts - defaulting to primary processor", MaxRetries);
                        return true; // Default to primary processor as last resort
                    }
                    
                    // Short delay before retry (non-blocking for other requests)
                    await Task.Delay(RetryDelay);
                }
            }
        }
        
        // This should never be reached due to the logic above, but just in case
        _logger.LogError("Unexpected end of decision logic - defaulting to primary processor");
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
                _logger.LogDebug("Using cached health check from Redis for {ProcessorKey}", processorKey);
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
                    _logger.LogDebug("Using cached health check from Redis for {ProcessorKey} (double-check)", processorKey);
                    var healthCheck = JsonSerializer.Deserialize<PaymentProcessorHealthCheck>(cachedData!, JsonOptions);
                    return healthCheck;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read health check from Redis cache during double-check for {ProcessorKey}", processorKey);
            }

            _logger.LogDebug("Fetching fresh health check for {ProcessorKey}", processorKey);
            
            // Fetch fresh health check
            var health = await healthCheckFunc();
            
            // Cache the result in Redis with expiration
            if (health != null)
            {
                try
                {
                    var jsonData = JsonSerializer.Serialize(health, JsonOptions);
                    await _redis.StringSetAsync(cacheKey, jsonData, CacheExpiry);
                    _logger.LogDebug("Cached health check in Redis for {ProcessorKey}", processorKey);
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
