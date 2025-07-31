using System.Text;
using System.Text.Json;
using Rinha.Models;

namespace Rinha.Services;

public record PaymentResult(bool Success, string ProcessorUsed);

public class PaymentService(IHttpClientFactory httpClientFactory, ILogger<PaymentService> logger,
    PaymentSummaryService summaryService, DecisionService decisionService, string defaultProcessorUrl, string fallbackProcessorUrl)
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<PaymentService> _logger = logger;
    private readonly PaymentSummaryService _summaryService = summaryService;
    private readonly DecisionService _decisionService = decisionService;
    private readonly string _defaultProcessorUrl = defaultProcessorUrl;
    private readonly string _fallbackProcessorUrl = fallbackProcessorUrl;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    private const int MaxRetries = 2; // Reduced from 3 for faster performance
    private static readonly TimeSpan[] RetryDelays = 
    {
        TimeSpan.FromMilliseconds(25),  // Reduced from 100ms
        TimeSpan.FromMilliseconds(100)  // Reduced from 500ms (removed 1s delay)
    };

    public async Task<bool> ProcessPaymentAsync(PaymentRequest paymentRequest)
    {
        var paymentData = new
        {
            correlationId = paymentRequest.CorrelationId,
            amount = paymentRequest.Amount,
            requestedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        };

        // Retry the entire payment process with exponential backoff
        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            var result = await TryProcessPaymentWithFallback(paymentData, paymentRequest.Amount, attempt);
            if (result.Success)
            {
                // Record success in circuit breaker
                await _decisionService.RecordSuccessAsync(result.ProcessorUsed);
                
                // Record in Redis only when we know which processor actually processed it
                await _summaryService.IncrementPaymentAsync(result.ProcessorUsed, paymentRequest.Amount);
                _logger.LogInformation("Payment {CorrelationId} successfully processed by {ProcessorType} processor", 
                    paymentRequest.CorrelationId, result.ProcessorUsed);
                return true;
            }

            // If this wasn't the last attempt, wait before retrying
            if (attempt < MaxRetries - 1)
            {
                var delay = RetryDelays[attempt];
                _logger.LogWarning("Payment attempt {Attempt} failed for correlation ID: {CorrelationId}, retrying in {Delay}ms", 
                    attempt + 1, paymentRequest.CorrelationId, delay.TotalMilliseconds);
                await Task.Delay(delay);
            }
        }

        _logger.LogError("Payment failed after {MaxRetries} attempts for correlation ID: {CorrelationId}", 
            MaxRetries, paymentRequest.CorrelationId);
        return false;
    }

    private async Task<PaymentResult> TryProcessPaymentWithFallback(object paymentData, decimal amount, int attemptNumber)
    {
        // Use DecisionService to determine the preferred processor
        bool preferDefaultProcessor = await _decisionService.DecidePaymentProcessor();
        
        string primaryProcessorUrl;
        string primaryProcessorType;
        string fallbackProcessorUrl_local;
        string fallbackProcessorType;
        
        if (preferDefaultProcessor)
        {
            primaryProcessorUrl = _defaultProcessorUrl;
            primaryProcessorType = "default";
            fallbackProcessorUrl_local = _fallbackProcessorUrl;
            fallbackProcessorType = "fallback";
        }
        else
        {
            primaryProcessorUrl = _fallbackProcessorUrl;
            primaryProcessorType = "fallback";
            fallbackProcessorUrl_local = _defaultProcessorUrl;
            fallbackProcessorType = "default";
        }
        
        // Try primary processor first
        if (await TryProcessPaymentCall(primaryProcessorUrl, paymentData))
        {
            return new PaymentResult(true, primaryProcessorType);
        }
        
        // Record failure for primary processor circuit breaker
        await _decisionService.RecordFailureAsync(primaryProcessorType);
        
        _logger.LogWarning("{ProcessorType} processor failed (attempt {Attempt}), trying {FallbackType} for correlation ID: {CorrelationId}", 
            primaryProcessorType, attemptNumber + 1, fallbackProcessorType, paymentData.GetType().GetProperty("correlationId")?.GetValue(paymentData));
        
        // Try fallback processor
        if (await TryProcessPaymentCall(fallbackProcessorUrl_local, paymentData))
        {
            return new PaymentResult(true, fallbackProcessorType);
        }

        // Record failure for fallback processor circuit breaker
        await _decisionService.RecordFailureAsync(fallbackProcessorType);

        _logger.LogWarning("Both processors failed on attempt {Attempt} for correlation ID: {CorrelationId}", 
            attemptNumber + 1, paymentData.GetType().GetProperty("correlationId")?.GetValue(paymentData));
        return new PaymentResult(false, "none");
    }

    private async Task<bool> TryProcessPaymentCall(string processorUrl, object paymentData)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var json = JsonSerializer.Serialize(paymentData, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"{processorUrl}/payments", content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Payment processed successfully for correlation ID: {CorrelationId}",
                    paymentData.GetType().GetProperty("correlationId")?.GetValue(paymentData));
                return true;
            }
            else
            {
                _logger.LogWarning("Payment processor failed for correlation ID: {CorrelationId}. Status: {StatusCode}",
                    paymentData.GetType().GetProperty("correlationId")?.GetValue(paymentData), response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Payment processor error for correlation ID: {CorrelationId}",
                paymentData.GetType().GetProperty("correlationId")?.GetValue(paymentData));
            return false;
        }
    }
}
