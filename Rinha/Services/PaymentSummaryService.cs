using StackExchange.Redis;
using Rinha.Models;

namespace Rinha.Services;

public class PaymentSummaryService
{
    private readonly IDatabase _database;
    private readonly ILogger<PaymentSummaryService> _logger;

    private const string DefaultRequestsKey = "payment_summary:default:requests";
    private const string DefaultAmountKey = "payment_summary:default:amount";
    private const string FallbackRequestsKey = "payment_summary:fallback:requests";
    private const string FallbackAmountKey = "payment_summary:fallback:amount";

    public PaymentSummaryService(IConnectionMultiplexer redis, ILogger<PaymentSummaryService> logger)
    {
        _database = redis.GetDatabase();
        _logger = logger;
    }

    public async Task IncrementPaymentAsync(string processorType, decimal amount)
    {
        try
        {
            var requestsKey = processorType.ToLower() == "default" ? DefaultRequestsKey : FallbackRequestsKey;
            var amountKey = processorType.ToLower() == "default" ? DefaultAmountKey : FallbackAmountKey;

            // Use pipeline for atomic operations
            var batch = _database.CreateBatch();
            var incrementRequests = batch.StringIncrementAsync(requestsKey);
            var incrementAmount = batch.StringIncrementAsync(amountKey, (double)amount);
            
            batch.Execute();

            await Task.WhenAll(incrementRequests, incrementAmount);

            _logger.LogDebug("Incremented {ProcessorType} payment: requests={Requests}, amount={Amount}",
                processorType, await incrementRequests, await incrementAmount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to increment payment summary for processor {ProcessorType}", processorType);
        }
    }

    public async Task<SummaryResponse> GetSummaryAsync(DateTime from, DateTime to)
    {
        try
        {
            // For this implementation, we're returning total counts regardless of date range
            // In a real scenario, you might want to implement time-based keys or use Redis Streams
            
            var batch = _database.CreateBatch();
            var defaultRequests = batch.StringGetAsync(DefaultRequestsKey);
            var defaultAmount = batch.StringGetAsync(DefaultAmountKey);
            var fallbackRequests = batch.StringGetAsync(FallbackRequestsKey);
            var fallbackAmount = batch.StringGetAsync(FallbackAmountKey);
            
            batch.Execute();

            await Task.WhenAll(defaultRequests, defaultAmount, fallbackRequests, fallbackAmount);

            var summary = new SummaryResponse
            {
                Default = new PaymentProcessorSummary
                {
                    TotalRequests = (int)(defaultRequests.Result.HasValue ? (long)defaultRequests.Result : 0),
                    TotalAmount = (decimal)(defaultAmount.Result.HasValue ? (double)defaultAmount.Result : 0)
                },
                Fallback = new PaymentProcessorSummary
                {
                    TotalRequests = (int)(fallbackRequests.Result.HasValue ? (long)fallbackRequests.Result : 0),
                    TotalAmount = (decimal)(fallbackAmount.Result.HasValue ? (double)fallbackAmount.Result : 0)
                }
            };

            _logger.LogDebug("Retrieved payment summary: Default({DefaultRequests}, {DefaultAmount}), Fallback({FallbackRequests}, {FallbackAmount})",
                summary.Default.TotalRequests, summary.Default.TotalAmount,
                summary.Fallback.TotalRequests, summary.Fallback.TotalAmount);

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve payment summary");
            
            // Return empty summary on error
            return new SummaryResponse
            {
                Default = new PaymentProcessorSummary(),
                Fallback = new PaymentProcessorSummary()
            };
        }
    }

    public async Task ResetSummaryAsync()
    {
        try
        {
            var keys = new RedisKey[] { DefaultRequestsKey, DefaultAmountKey, FallbackRequestsKey, FallbackAmountKey };
            await _database.KeyDeleteAsync(keys);
            _logger.LogInformation("Payment summary reset successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset payment summary");
        }
    }
}
