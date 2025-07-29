using StackExchange.Redis;
using Rinha.Models;

namespace Rinha.Services;

public class PaymentSummaryService(IConnectionMultiplexer redis, ILogger<PaymentSummaryService> logger)
{
    private readonly IDatabase _database = redis.GetDatabase();
    private readonly ILogger<PaymentSummaryService> _logger = logger;

    private const string DefaultPaymentsKey = "payment_summary:default:payments";
    private const string FallbackPaymentsKey = "payment_summary:fallback:payments";

    public async Task IncrementPaymentAsync(string processorType, decimal amount)
    {
        try
        {
            var paymentsKey = processorType.ToLower() == "default" ? DefaultPaymentsKey : FallbackPaymentsKey;
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Store payment amount with timestamp as score in Redis sorted set
            // The member value combines timestamp and amount for uniqueness
            var memberValue = $"{timestamp}:{amount}";
            
            await _database.SortedSetAddAsync(paymentsKey, memberValue, timestamp);

            _logger.LogDebug("Added {ProcessorType} payment: amount={Amount}, timestamp={Timestamp}",
                processorType, amount, timestamp);
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
            var fromTimestamp = new DateTimeOffset(from).ToUnixTimeMilliseconds();
            var toTimestamp = new DateTimeOffset(to).ToUnixTimeMilliseconds();

            // Get payments from both processors within the time range
            var defaultPayments = await _database.SortedSetRangeByScoreAsync(
                DefaultPaymentsKey, fromTimestamp, toTimestamp);
            var fallbackPayments = await _database.SortedSetRangeByScoreAsync(
                FallbackPaymentsKey, fromTimestamp, toTimestamp);

            // Calculate totals for default processor
            var defaultTotalRequests = defaultPayments.Length;
            var defaultTotalAmount = CalculateTotalAmount(defaultPayments);

            // Calculate totals for fallback processor
            var fallbackTotalRequests = fallbackPayments.Length;
            var fallbackTotalAmount = CalculateTotalAmount(fallbackPayments);

            var summary = new SummaryResponse
            {
                Default = new PaymentProcessorSummary
                {
                    TotalRequests = defaultTotalRequests,
                    TotalAmount = defaultTotalAmount
                },
                Fallback = new PaymentProcessorSummary
                {
                    TotalRequests = fallbackTotalRequests,
                    TotalAmount = fallbackTotalAmount
                }
            };

            _logger.LogDebug("Retrieved payment summary for period {From} to {To}: Default({DefaultRequests}, {DefaultAmount}), Fallback({FallbackRequests}, {FallbackAmount})",
                from, to, summary.Default.TotalRequests, summary.Default.TotalAmount,
                summary.Fallback.TotalRequests, summary.Fallback.TotalAmount);

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve payment summary for period {From} to {To}", from, to);
            
            // Return empty summary on error
            return new SummaryResponse
            {
                Default = new PaymentProcessorSummary(),
                Fallback = new PaymentProcessorSummary()
            };
        }
    }

    private static decimal CalculateTotalAmount(RedisValue[] payments)
    {
        decimal total = 0;
        foreach (var payment in payments)
        {
            // Extract amount from the member value format "timestamp:amount"
            var parts = payment.ToString().Split(':');
            if (parts.Length >= 2 && decimal.TryParse(parts[1], out var amount))
            {
                total += amount;
            }
        }
        return total;
    }

    public async Task ResetSummaryAsync()
    {
        try
        {
            var keys = new RedisKey[] { DefaultPaymentsKey, FallbackPaymentsKey };
            await _database.KeyDeleteAsync(keys);
            _logger.LogInformation("Payment summary reset successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset payment summary");
        }
    }
}
