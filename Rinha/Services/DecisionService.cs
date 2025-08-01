using System.Collections.Concurrent;
using System.Text.Json;
using Rinha.Models;
using StackExchange.Redis;

namespace Rinha.Services;

public class DecisionService(PaymentHealthCheckService healthCheckService, ILogger<DecisionService> logger, IConnectionMultiplexer redis)
{
    private readonly PaymentHealthCheckService _healthCheckService = healthCheckService;
    private readonly ILogger<DecisionService> _logger = logger;
    private readonly IDatabase _redis = redis.GetDatabase();
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromSeconds(5);
    private const int LatencyThreshold = 500;

    // Circuit breaker settings
    private const int FailureThreshold = 5;
    private const int SuccessThreshold = 3;
    private static readonly TimeSpan OpenCircuitTimeout = TimeSpan.FromSeconds(5);
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Local lock to prevent multiple threads from fetching the same health check simultaneously
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _fetchLocks = new();

    // Decides which payment processor to use based on health checks, latency, and circuit breaker state
    // Returns true for default processor, false for fallback processor
    public async Task<bool> UsePrimaryProcessor()
    {
        // Fetch both circuit breaker states in parallel using pipeline for efficiency
        var defaultCircuitTask = GetCircuitBreakerStateAsync("default");
        var fallbackCircuitTask = GetCircuitBreakerStateAsync("fallback");
        
        var defaultCircuitState = await defaultCircuitTask;
        var fallbackCircuitState = await fallbackCircuitTask;
        
        // If default circuit is open, use fallback (if available)
        if (defaultCircuitState.State == CircuitBreakerState.Open)
        {
            if (fallbackCircuitState.State != CircuitBreakerState.Open)
            {
                _logger.LogDebug("DECISION_RESULT: Using fallback processor - default circuit breaker is OPEN");
                return false;
            }
            // Both circuits open - default to primary (lower fees) and let it fail fast
            _logger.LogWarning("DECISION_RESULT: Both circuits OPEN - defaulting to primary (will fail fast)");
            return true;
        }
        
        // If default circuit is half-open, check health first before testing with real traffic
        if (defaultCircuitState.State == CircuitBreakerState.HalfOpen)
        {
            var defaultHealthCheck = await GetCachedHealthCheckAsync("default", _healthCheckService.GetDefaultProcessorHealthAsync);
            if (defaultHealthCheck != null && !defaultHealthCheck.Failing)
            {
                _logger.LogDebug("DECISION_RESULT: Using default processor - circuit is HALF-OPEN and health check passed");
                return true;
            }

            _logger.LogDebug("DECISION_RESULT: Using fallback processor - default circuit is HALF-OPEN but health check failed");
            return false;
        }
        
        // If fallback circuit is open, prefer default
        if (fallbackCircuitState.State == CircuitBreakerState.Open)
        {
            _logger.LogDebug("DECISION_RESULT: Using default processor - fallback circuit breaker is OPEN");
            return true;
        }
        
        // If fallback circuit is half-open, check health first before testing
        if (fallbackCircuitState.State == CircuitBreakerState.HalfOpen)
        {
            var fallbackHealthCheck = await GetCachedHealthCheckAsync("fallback", _healthCheckService.GetFallbackProcessorHealthAsync);
            if (fallbackHealthCheck != null && !fallbackHealthCheck.Failing)
            {
                _logger.LogDebug("DECISION_RESULT: Using fallback processor - circuit is HALF-OPEN and health check passed, testing recovery");
                return false;
            }
            _logger.LogDebug("DECISION_RESULT: Using default processor - fallback is HALF-OPEN but health check failed");
            return true;
        }
        
        // Both circuits closed - proceed with health checks
        // Fetch both health checks in parallel
        var defaultHealthTask = GetCachedHealthCheckAsync("default", _healthCheckService.GetDefaultProcessorHealthAsync);
        var fallbackHealthTask = GetCachedHealthCheckAsync("fallback", _healthCheckService.GetFallbackProcessorHealthAsync);
        
        var defaultHealth = await defaultHealthTask;
        
        // If default is healthy and fast, use it immediately (don't wait for fallback)
        if (defaultHealth != null && !defaultHealth.Failing && defaultHealth.MinResponseTime < LatencyThreshold)
        {
            _logger.LogDebug("DECISION_RESULT: Using default processor - healthy and low latency ({Latency}ms)", defaultHealth.MinResponseTime);
            return true;
        }
        
        // Default is problematic, wait for fallback result
        var fallbackHealth = await fallbackHealthTask;
        
        // Use fallback if it's healthy
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
            
            // Cache the result in Redis with expiration but don't await
            if (health != null)
            {
                _ = Task.Run(async () =>
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
                });
            }

            return health;
        }
        finally
        {
            semaphore.Release();
        }
    }

    // Records a successful payment for circuit breaker tracking
    public async Task RecordSuccessAsync(string processorType)
    {
        try
        {
            var circuitKey = $"circuit_breaker:{processorType}";
            var circuitData = await GetCircuitBreakerStateAsync(processorType);
            
            if (circuitData.State == CircuitBreakerState.HalfOpen)
            {
                circuitData.SuccessCount++;
                _logger.LogDebug("Circuit breaker success recorded for {ProcessorType}: {SuccessCount}/{RequiredSuccesses}", 
                    processorType, circuitData.SuccessCount, SuccessThreshold);
                
                if (circuitData.SuccessCount >= SuccessThreshold)
                {
                    // Close the circuit. Processor is healthy again
                    circuitData.State = CircuitBreakerState.Closed;
                    circuitData.FailureCount = 0;
                    circuitData.SuccessCount = 0;
                    circuitData.LastStateChange = DateTime.UtcNow;
                    _logger.LogInformation("Circuit breaker CLOSED for {ProcessorType} - processor recovered", processorType);
                }
                
                var jsonData = JsonSerializer.Serialize(circuitData, JsonOptions);
                await _redis.StringSetAsync(circuitKey, jsonData, TimeSpan.FromMinutes(10));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record success for circuit breaker {ProcessorType}", processorType);
        }
    }

    // Records a failure for circuit breaker tracking
    public async Task RecordFailureAsync(string processorType)
    {
        try
        {
            var circuitKey = $"circuit_breaker:{processorType}";
            var circuitData = await GetCircuitBreakerStateAsync(processorType);
            
            if (circuitData.State == CircuitBreakerState.Closed || circuitData.State == CircuitBreakerState.HalfOpen)
            {
                circuitData.FailureCount++;
                circuitData.LastFailureTime = DateTime.UtcNow;
                
                _logger.LogDebug("Circuit breaker failure recorded for {ProcessorType}: {FailureCount}/{FailureThreshold}", 
                    processorType, circuitData.FailureCount, FailureThreshold);
                
                if (circuitData.FailureCount >= FailureThreshold)
                {
                    // Open the circuit. Processor is unhealthy
                    circuitData.State = CircuitBreakerState.Open;
                    circuitData.SuccessCount = 0;
                    circuitData.LastStateChange = DateTime.UtcNow;
                    _logger.LogWarning("Circuit breaker OPENED for {ProcessorType} - too many failures ({FailureCount})", 
                        processorType, circuitData.FailureCount);
                }
                
                var jsonData = JsonSerializer.Serialize(circuitData, JsonOptions);
                await _redis.StringSetAsync(circuitKey, jsonData, TimeSpan.FromMinutes(10));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record failure for circuit breaker {ProcessorType}", processorType);
        }
    }

    // Gets the current circuit breaker state for a processor
    private async Task<CircuitBreakerData> GetCircuitBreakerStateAsync(string processorType)
    {
        var circuitKey = $"circuit_breaker:{processorType}";
        
        try
        {
            var cachedData = await _redis.StringGetAsync(circuitKey);
            if (cachedData.HasValue)
            {
                var circuitData = JsonSerializer.Deserialize<CircuitBreakerData>(cachedData!, JsonOptions);
                if (circuitData != null)
                {
                    // Check if we should transition from Open to Half-Open
                    if (circuitData.State == CircuitBreakerState.Open && 
                        DateTime.UtcNow - circuitData.LastStateChange > OpenCircuitTimeout)
                    {
                        circuitData.State = CircuitBreakerState.HalfOpen;
                        circuitData.SuccessCount = 0;
                        circuitData.LastStateChange = DateTime.UtcNow;
                        
                        _logger.LogInformation("Circuit breaker transitioned to HALF-OPEN for {ProcessorType} - testing recovery", processorType);
                        
                        var jsonData = JsonSerializer.Serialize(circuitData, JsonOptions);
                        await _redis.StringSetAsync(circuitKey, jsonData, TimeSpan.FromMinutes(10));
                    }
                    
                    return circuitData;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read circuit breaker state for {ProcessorType}", processorType);
        }
        
        // Default state if no data found or error
        return new CircuitBreakerData();
    }
}

public enum CircuitBreakerState
{
    Closed,
    Open,
    HalfOpen
}

public class CircuitBreakerData
{
    public CircuitBreakerState State { get; set; } = CircuitBreakerState.Closed;
    public int FailureCount { get; set; } = 0;
    public int SuccessCount { get; set; } = 0;
    public DateTime LastFailureTime { get; set; } = DateTime.MinValue;
    public DateTime LastStateChange { get; set; } = DateTime.UtcNow;
}